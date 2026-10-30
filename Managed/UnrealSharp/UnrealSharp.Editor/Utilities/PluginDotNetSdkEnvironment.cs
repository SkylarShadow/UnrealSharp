using System.Runtime.InteropServices;

namespace UnrealSharp.Editor.Utilities;

internal static class PluginDotNetSdkEnvironment
{
    private static readonly SemaphoreSlim EnvironmentLock = new(1, 1);

    private static readonly string[] VariableNames =
    [
        "DOTNET_ROOT",
        "DOTNET_ROOT_X64",
        "DOTNET_ROOT_ARM64",
        "DOTNET_HOST_PATH",
        "DOTNET_MULTILEVEL_LOOKUP"
    ];

    public static async Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await EnvironmentLock.WaitAsync().ConfigureAwait(false);
        Dictionary<string, string?> originalValues = VariableNames.ToDictionary(
            static name => name,
            static name => Environment.GetEnvironmentVariable(name),
            StringComparer.Ordinal);

        try
        {
            (string dotNetRoot, string dotNetExecutable, string architecture) = ResolvePluginSdk();
            Environment.SetEnvironmentVariable("DOTNET_ROOT", dotNetRoot);
            Environment.SetEnvironmentVariable("DOTNET_ROOT_X64", architecture == "x64" ? dotNetRoot : null);
            Environment.SetEnvironmentVariable("DOTNET_ROOT_ARM64", architecture == "arm64" ? dotNetRoot : null);
            Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", dotNetExecutable);
            Environment.SetEnvironmentVariable("DOTNET_MULTILEVEL_LOOKUP", "0");

            return await operation().ConfigureAwait(false);
        }
        finally
        {
            foreach ((string name, string? value) in originalValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }

            EnvironmentLock.Release();
        }
    }

    private static (string Root, string Executable, string Architecture) ResolvePluginSdk()
    {
        IEnumerable<string?> candidates = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.Location)
            .Append(AppDomain.CurrentDomain.GetData("APP_CONTEXT_BASE_DIRECTORY") as string)
            .Append(Environment.CurrentDirectory);

        DirectoryInfo? pluginRoot = candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(FindPluginRoot)
            .FirstOrDefault(root => root != null);
        if (pluginRoot == null)
        {
            throw new DirectoryNotFoundException("Could not locate the UnrealSharp plugin root.");
        }

        string platform = OperatingSystem.IsWindows()
            ? "win"
            : OperatingSystem.IsMacOS()
                ? "osx"
                : OperatingSystem.IsLinux()
                    ? "linux"
                    : throw new PlatformNotSupportedException("Unsupported .NET SDK host platform.");

        string architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException(
                $"Unsupported .NET SDK host architecture: {RuntimeInformation.ProcessArchitecture}")
        };

        string dotNetRoot = Path.Combine(pluginRoot.FullName, "ThirdParty", "DotNet", "sdk", $"{platform}-{architecture}");
        string executable = Path.Combine(dotNetRoot, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        if (!File.Exists(executable) || !Directory.Exists(Path.Combine(dotNetRoot, "sdk")))
        {
            throw new DirectoryNotFoundException($"The UnrealSharp plugin .NET SDK was not found at '{dotNetRoot}'.");
        }

        return (dotNetRoot, executable, architecture);
    }

    private static DirectoryInfo? FindPluginRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        DirectoryInfo? directory;
        try
        {
            directory = Directory.Exists(path)
                ? new DirectoryInfo(path)
                : new FileInfo(path).Directory;
        }
        catch (ArgumentException)
        {
            return null;
        }

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UnrealSharp.uplugin")))
            {
                return directory;
            }

            string projectPlugin = Path.Combine(directory.FullName, "Plugins", "UnrealSharp");
            if (File.Exists(Path.Combine(projectPlugin, "UnrealSharp.uplugin")))
            {
                return new DirectoryInfo(projectPlugin);
            }

            directory = directory.Parent;
        }

        return null;
    }
}

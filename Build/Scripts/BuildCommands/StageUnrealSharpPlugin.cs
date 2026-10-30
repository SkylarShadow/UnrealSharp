using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AutomationTool;
using UnrealBuildTool;
using UnrealSharp.Automation.Utilities;

namespace UnrealSharp.Automation.BuildCommands;

[Help("Stages a self-contained .NET runtime and UnrealSharp managed assemblies into the plugin directory.")]
[Help("UEBuildConfig=<Config>", "Optional. Defaults to Development.")]
[Help("UETargetType=<Type>", "Optional. Defaults to Editor.")]
[Help("TargetPlatform=<Platform>", "Optional. Defaults to Win64.")]
[Help("TargetArchitecture=<Arch>", "Optional. Defaults to X64.")]
public class StageUnrealSharpPlugin : BuildCommand
{
    public override void ExecuteBuild()
    {
        UnrealTargetConfiguration BuildConfiguration = ParseEnumParam("UEBuildConfig", UnrealTargetConfiguration.Development);
        TargetType TargetType = ParseEnumParam("UETargetType", TargetType.Editor);
        UnrealTargetPlatform TargetPlatform = UnrealTargetPlatform.Parse(ParseParamValue("TargetPlatform", UnrealTargetPlatform.Win64.ToString()));
        UnrealArch TargetArchitecture = UnrealArch.Parse(ParseParamValue("TargetArchitecture", UnrealArch.X64.ToString()));

        string PluginRoot = this.GetUnrealSharpRootFolder();
        DotNetUtilities.ConfigurePluginDotNetRoot(PluginRoot);
        DotNetSdkUtilities.CopyGlobalJson(this);
        ManagedBuildToolsUtilities.EnsureStaged(PluginRoot, BuildConfiguration, forceBuild: true);
        string RuntimeIdentifier = DotNetSdkUtilities.GetDotNetRuntimeIdentifier(TargetPlatform, TargetArchitecture);
        string RuntimePackageFolder = Path.Combine(PluginRoot, "ThirdParty", "DotNet", RuntimeIdentifier, DotNetUtilities.RuntimeFamilyVersion);
        if (!Directory.Exists(RuntimePackageFolder))
        {
            throw new AutomationException($"The versioned .NET runtime payload was not found at '{RuntimePackageFolder}'.");
        }
        string RuntimeVersion = ReadRuntimeVersion(RuntimePackageFolder);
        ValidateRuntimePayload(RuntimePackageFolder, TargetPlatform);

        string PublishFolder = PathUtilities.BuildOutputPath(PluginRoot);
        string ManagedPublishFolder = Path.Combine(PluginRoot, "Intermediate", "Managed", DotNetUtilities.Version, RuntimeIdentifier);
        ClearPublishFolder(ManagedPublishFolder);

        IList<string> Arguments =
        [
            "--runtime", RuntimeIdentifier,
            "-p:UseDefaultOutputPath=true",
            // The native host uses hostfxr_initialize_for_dotnet_command_line for the
            // plugin assembly. The published deps.json must therefore carry the
            // runtimepack assets (coreclr.dll, hostpolicy.dll, etc.) instead of relying
            // on a shared machine-wide framework.
            "-p:PublishSelfContained=true",
            "-p:PublishTrimmed=false",
            "-p:PublishReadyToRun=false",
            $"-p:RuntimeFrameworkVersion={RuntimeVersion}",
            $"-p:UETargetType={TargetType}",
            $"-p:UEBuildConfig={BuildConfiguration}",
            $"-p:PublishDir=\"{ManagedPublishFolder}\"",
        ];

        string ManagedRoot = Path.Combine(PluginRoot, "Managed", "UnrealSharp");
        string EditorProjectFolder = Path.Combine(ManagedRoot, "UnrealSharp.Editor");
        string PluginsProjectFolder = Path.Combine(ManagedRoot, "UnrealSharp.Plugins");
        global::UnrealSharp.Automation.BuildCommands.BuildSolution.RunBuild([EditorProjectFolder, PluginsProjectFolder], BuildConfiguration, publish: true, Arguments);

        ValidatePublishedRuntimeConfig(ManagedPublishFolder, "UnrealSharp.Editor", RuntimeVersion);
        ValidatePublishedRuntimeConfig(ManagedPublishFolder, "UnrealSharp.Plugins", RuntimeVersion);
        ValidatePublishedAssembly(ManagedPublishFolder, "UnrealSharp.Editor");
        ValidatePublishedAssembly(ManagedPublishFolder, "UnrealSharp.Plugins");

        ClearPublishFolder(PublishFolder);
        CopyDirectory(RuntimePackageFolder, PublishFolder);
        // Keep the downloaded runtime authoritative. Only managed outputs and
        // application-specific runtimeconfigs come from dotnet publish.
        CopyDirectory(ManagedPublishFolder, PublishFolder, RuntimePackageFolder);

        LoggerUtilities.LogUnrealSharpInfo($"Staged the versioned .NET runtime payload ({RuntimeIdentifier}) and current managed assemblies at '{PublishFolder}'.");
    }

    private T ParseEnumParam<T>(string parameterName, T defaultValue) where T : struct, Enum
    {
        string Value = ParseParamValue(parameterName, defaultValue.ToString());
        return Enum.Parse<T>(Value, ignoreCase: true);
    }

    private static void ClearPublishFolder(string publishFolder)
    {
        if (Directory.Exists(publishFolder))
        {
            Directory.Delete(publishFolder, recursive: true);
        }

        Directory.CreateDirectory(publishFolder);
    }

    private static void CopyDirectory(string sourceFolder, string destinationFolder, string? runtimePackageFolder = null)
    {
        Directory.CreateDirectory(destinationFolder);

        foreach (string DirectoryPath in Directory.GetDirectories(sourceFolder, "*", SearchOption.AllDirectories))
        {
            string RelativePath = Path.GetRelativePath(sourceFolder, DirectoryPath);
            Directory.CreateDirectory(Path.Combine(destinationFolder, RelativePath));
        }

        foreach (string FilePath in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
        {
            string RelativePath = Path.GetRelativePath(sourceFolder, FilePath);
            if (runtimePackageFolder != null &&
                !RelativePath.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(Path.Combine(runtimePackageFolder, RelativePath)))
            {
                continue;
            }
            string DestinationPath = Path.Combine(destinationFolder, RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(DestinationPath)!);
            File.Copy(FilePath, DestinationPath, overwrite: true);
        }
    }

    private static string ReadRuntimeVersion(string runtimeFolder)
    {
        string versionFile = Path.Combine(runtimeFolder, ".version");
        string[] lines = File.ReadAllLines(versionFile);
        if (lines.Length < 2 ||
            !Version.TryParse(lines[1].Split('-')[0], out Version? version) ||
            version.Major.ToString() + "." + version.Minor != DotNetUtilities.RuntimeFamilyVersion)
        {
            throw new AutomationException($"Invalid .NET 11 runtime version in '{versionFile}'.");
        }

        return lines[1].Trim();
    }

    private static void ValidateRuntimePayload(string runtimeFolder, UnrealTargetPlatform platform)
    {
        string[] runtimeFiles = platform == UnrealTargetPlatform.Win64
            ? ["hostfxr.dll", "hostpolicy.dll", "coreclr.dll", "System.Private.CoreLib.dll"]
            : platform == UnrealTargetPlatform.Mac
                ? ["libhostfxr.dylib", "libhostpolicy.dylib", "libcoreclr.dylib", "System.Private.CoreLib.dll"]
                : ["libhostfxr.so", "libhostpolicy.so", "libcoreclr.so", "System.Private.CoreLib.dll"];

        foreach (string file in runtimeFiles)
        {
            if (!File.Exists(Path.Combine(runtimeFolder, file)))
            {
                throw new AutomationException($"The downloaded runtime payload is missing '{file}' in '{runtimeFolder}'.");
            }
        }
    }

    private static void ValidatePublishedRuntimeConfig(string publishFolder, string assemblyName, string expectedVersion)
    {
        string configPath = Path.Combine(publishFolder, assemblyName + ".runtimeconfig.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath));
        JsonElement frameworks = document.RootElement.GetProperty("runtimeOptions").GetProperty("includedFrameworks");
        foreach (JsonElement framework in frameworks.EnumerateArray())
        {
            if (framework.GetProperty("name").GetString() == "Microsoft.NETCore.App" &&
                framework.GetProperty("version").GetString() == expectedVersion)
            {
                return;
            }
        }

        throw new AutomationException($"'{configPath}' does not use the downloaded .NET runtime {expectedVersion}.");
    }

    private static void ValidatePublishedAssembly(string publishFolder, string assemblyName)
    {
        string assemblyPath = Path.Combine(publishFolder, assemblyName + ".dll");
        if (!File.Exists(assemblyPath))
        {
            throw new AutomationException($"Managed assembly was not published at '{assemblyPath}'.");
        }
    }
}

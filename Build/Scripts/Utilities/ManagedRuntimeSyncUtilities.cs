using System;
using System.IO;
using UnrealBuildTool;

namespace UnrealSharp.Automation.Utilities;

public static class ManagedRuntimeSyncUtilities
{
    private const string PendingSuffix = ".pending";

    public static void SyncPluginBootstrap(string pluginRoot, UnrealTargetConfiguration buildConfiguration)
    {
        string targetFramework = DotNetUtilities.Version;
        string configuration = buildConfiguration.GetDotNetBuildConfiguration();
        string projectFolder = Path.Combine(pluginRoot, "Managed", "UnrealSharp", "UnrealSharp.Plugins");
        string outputFolder = Path.Combine(projectFolder, "bin", configuration, targetFramework);
        string destinationFolder = Path.Combine(pluginRoot, "Binaries", "Managed", targetFramework);

        Directory.CreateDirectory(destinationFolder);
        ApplyPendingFile(destinationFolder, "UnrealSharp.Plugins.dll");
        ApplyPendingFile(destinationFolder, "UnrealSharp.Plugins.pdb");

        string builtAssembly = Path.Combine(outputFolder, "UnrealSharp.Plugins.dll");
        if (NeedsBuild(pluginRoot, builtAssembly))
        {
            BuildCommands.BuildSolution.RunBuild(
                projectFolder,
                buildConfiguration,
                publish: false,
                [
                    "-p:UseDefaultOutputPath=true",
                    "-p:UETargetType=Editor",
                    $"-p:UEBuildConfig={buildConfiguration}"
                ]);
        }

        CopyOrDefer(outputFolder, destinationFolder, "UnrealSharp.Plugins.dll");
        CopyOrDefer(outputFolder, destinationFolder, "UnrealSharp.Plugins.pdb", required: false);
    }

    private static bool NeedsBuild(string pluginRoot, string builtAssembly)
    {
        if (!File.Exists(builtAssembly))
        {
            return true;
        }

        DateTime builtAt = File.GetLastWriteTimeUtc(builtAssembly);
        string managedRoot = Path.Combine(pluginRoot, "Managed");
        if (HasNewerBuildInput(managedRoot, builtAt))
        {
            return true;
        }

        string generatedBindings = Path.Combine(pluginRoot, "Intermediate", "UnrealSharp", "UHT");
        if (Directory.Exists(generatedBindings) && HasNewerBuildInput(generatedBindings, builtAt))
        {
            return true;
        }

        foreach (string propsFile in Directory.EnumerateFiles(pluginRoot, "*.props", SearchOption.TopDirectoryOnly))
        {
            if (File.GetLastWriteTimeUtc(propsFile) > builtAt)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasNewerBuildInput(string folder, DateTime builtAt)
    {
        foreach (string file in Directory.EnumerateFiles(folder))
        {
            string extension = Path.GetExtension(file);
            if ((extension == ".cs" || extension == ".csproj" || extension == ".props" || extension == ".targets" ||
                 Path.GetFileName(file) == "global.json") && File.GetLastWriteTimeUtc(file) > builtAt)
            {
                return true;
            }
        }

        foreach (string child in Directory.EnumerateDirectories(folder))
        {
            string name = Path.GetFileName(child);
            if (name.Equals("bin", StringComparison.OrdinalIgnoreCase) || name.Equals("obj", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (HasNewerBuildInput(child, builtAt))
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyPendingFile(string destinationFolder, string fileName)
    {
        string pending = Path.Combine(destinationFolder, fileName + PendingSuffix);
        if (!File.Exists(pending))
        {
            return;
        }

        string destination = Path.Combine(destinationFolder, fileName);
        try
        {
            File.Move(pending, destination, overwrite: true);
            LoggerUtilities.LogUnrealSharpInfo($"Applied pending managed bootstrap update '{destination}'.");
        }
        catch (IOException)
        {
            // The editor can invoke BuildUserSolution again while the old bootstrap
            // assembly is still loaded. Keep the pending file for the next startup.
        }
    }

    private static void CopyOrDefer(string sourceFolder, string destinationFolder, string fileName, bool required = true)
    {
        string source = Path.Combine(sourceFolder, fileName);
        if (!File.Exists(source))
        {
            if (required)
            {
                throw new FileNotFoundException($"Managed bootstrap file was not produced at '{source}'.", source);
            }

            return;
        }

        string destination = Path.Combine(destinationFolder, fileName);
        if (File.Exists(destination) && File.GetLastWriteTimeUtc(destination) >= File.GetLastWriteTimeUtc(source))
        {
            return;
        }
        try
        {
            File.Copy(source, destination, overwrite: true);
            File.Delete(destination + PendingSuffix);
            LoggerUtilities.LogUnrealSharpInfo($"Synchronized managed bootstrap file '{destination}'.");
        }
        catch (IOException exception)
        {
            string pending = destination + PendingSuffix;
            File.Copy(source, pending, overwrite: true);
            LoggerUtilities.LogUnrealSharpWarning(
                $"Managed bootstrap file '{destination}' is in use. Staged '{pending}' for the next editor start. {exception.Message}");
        }
    }
}

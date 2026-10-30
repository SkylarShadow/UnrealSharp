using System.Collections.Generic;
using System.IO;
using UnrealBuildTool;

namespace UnrealSharp.Automation.Utilities;

public static class ManagedBuildToolsUtilities
{
    private const string ToolTargetFramework = "netstandard2.0";

    public static void EnsureStaged(string pluginRoot, UnrealTargetConfiguration buildConfiguration, bool forceBuild)
    {
        string destination = Path.Combine(pluginRoot, "Binaries", "Managed", ToolTargetFramework);
        string managedRoot = Path.Combine(pluginRoot, "Managed", "UnrealSharp");
        string configuration = buildConfiguration.GetDotNetBuildConfiguration();

        List<(string ProjectFolder, string OutputFolder, string[] Files)> projects =
        [
            (
                Path.Combine(managedRoot, "UnrealSharp.GlueGenerator", "UnrealSharp.GlueGenerator"),
                Path.Combine(managedRoot, "UnrealSharp.GlueGenerator", "UnrealSharp.GlueGenerator", "bin", configuration, ToolTargetFramework),
                ["Newtonsoft.Json.dll", "UnrealSharp.GlueGenerator.dll"]
            ),
            (
                Path.Combine(managedRoot, "UnrealSharp.Analyzers"),
                Path.Combine(managedRoot, "UnrealSharp.Analyzers", "bin", configuration, ToolTargetFramework),
                ["UnrealSharp.Analyzers.dll"]
            ),
            (
                Path.Combine(managedRoot, "UnrealSharp.CodeFixer"),
                Path.Combine(managedRoot, "UnrealSharp.CodeFixer", "bin", configuration, ToolTargetFramework),
                ["UnrealSharp.CodeFixer.dll"]
            ),
            (
                Path.Combine(managedRoot, "UnrealSharp.SourceGenerators"),
                Path.Combine(managedRoot, "UnrealSharp.SourceGenerators", "bin", configuration, ToolTargetFramework),
                ["UnrealSharp.SourceGenerators.dll"]
            )
        ];

        bool missingFiles = false;
        foreach ((_, _, string[] files) in projects)
        {
            foreach (string file in files)
            {
                if (!File.Exists(Path.Combine(destination, file)))
                {
                    missingFiles = true;
                    break;
                }
            }

            if (missingFiles)
            {
                break;
            }
        }

        if (!forceBuild && !missingFiles)
        {
            return;
        }

        LoggerUtilities.LogUnrealSharpInfo("Building UnrealSharp compile-time tools.");
        foreach ((string projectFolder, _, _) in projects)
        {
            BuildCommands.BuildSolution.RunBuild(
                projectFolder,
                buildConfiguration,
                publish: false,
                ["-p:UseDefaultOutputPath=true"]);
        }

        Directory.CreateDirectory(destination);
        foreach ((_, string outputFolder, string[] files) in projects)
        {
            foreach (string file in files)
            {
                string source = Path.Combine(outputFolder, file);
                if (!File.Exists(source))
                {
                    throw new FileNotFoundException($"Managed build tool '{file}' was not produced at '{source}'.", source);
                }

                File.Copy(source, Path.Combine(destination, file), overwrite: true);
            }
        }

        LoggerUtilities.LogUnrealSharpInfo($"Staged UnrealSharp compile-time tools at '{destination}'.");
    }
}

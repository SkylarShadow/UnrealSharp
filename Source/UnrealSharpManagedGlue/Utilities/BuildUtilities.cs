using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnrealBuildTool;
using UnrealSharp.Automation.Utilities;

namespace UnrealSharpManagedGlue.Utilities;

public static class BuildUtilities
{
    public static void BuildBindings()
    {
        if (GeneratorStatics.TargetType != TargetRules.TargetType.Editor)
        {
            return;
        }
        //TODO: All platform
        // Check if the UnrealSharp bindings are already staged
        string BindingsAssemblyPath = Path.Combine(
            GeneratorStatics.PluginDirectory,
            "Binaries",
            "Managed",
            DotNetUtilities.Version,
            "UnrealSharp.Plugins.dll");
        bool BindingsAreMissing = !File.Exists(BindingsAssemblyPath);

        if (!FileExporter.HasModifiedEngineGlue && !BindingsAreMissing)
        {
            return;
        }

        string Reason = BindingsAreMissing
            ? "UnrealSharp bindings are missing. Staging the self-contained managed runtime..."
            : "Engine glue has been modified since the last build. Restaging UnrealSharp bindings...";
        ConsoleUtilities.Log(Reason);

        List<KeyValuePair<string, string>> actionArgs =
        [
            new("UEBuildConfig", GeneratorStatics.TargetConfiguration.ToString()),
            new("UETargetType", GeneratorStatics.TargetType.ToString()),
            new("TargetPlatform", GetHostPlatform()),
            new("TargetArchitecture", GetHostArchitecture())
        ];

        UnrealSharpAutomationUtilities.InvokeUnrealSharpAutomation("StageUnrealSharpPlugin", actionArgs);
    }

    private static string GetHostPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return UnrealTargetPlatform.Win64.ToString();
        }

        if (OperatingSystem.IsMacOS())
        {
            return UnrealTargetPlatform.Mac.ToString();
        }

        if (OperatingSystem.IsLinux())
        {
            return UnrealTargetPlatform.Linux.ToString();
        }

        throw new PlatformNotSupportedException("UnrealSharp staging does not support this host platform.");
    }

    private static string GetHostArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => UnrealArch.X64.ToString(),
            Architecture.Arm64 => UnrealArch.Arm64.ToString(),
            _ => throw new PlatformNotSupportedException($"UnrealSharp staging does not support {RuntimeInformation.ProcessArchitecture}.")
        };
    }
    
    public static void GenerateUserSolution()
    {
        if (GeneratorStatics.TargetType != TargetRules.TargetType.Editor)
        {
            return;
        }
        
        List<KeyValuePair<string, string>> ActionArgs =
        [
            new("TargetType", GeneratorStatics.TargetType.ToString()),
            new("TargetConfiguration", GeneratorStatics.TargetConfiguration.ToString())
        ];
        UnrealSharpAutomationUtilities.InvokeUnrealSharpAutomation("GenerateUserSolution", ActionArgs);
    }
}

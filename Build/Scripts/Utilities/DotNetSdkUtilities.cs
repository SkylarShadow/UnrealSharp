using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutomationTool;
using UnrealBuildTool;

namespace UnrealSharp.Automation.Utilities;

public static class DotNetSdkUtilities
{
    public static string GetDotNetRuntimeIdentifier(UnrealTargetPlatform platform, UnrealArch architecture)
    {
        string PlatformPart = GetPlatformIdentifier(platform);
        string ArchitecturePart = GetArchitectureIdentifier(architecture);
        return $"{PlatformPart}-{ArchitecturePart}";
    }

    public static string GetPlatformIdentifier(UnrealTargetPlatform platform)
    {
        if (platform == UnrealTargetPlatform.Win64)
        {
            return "win";
        }

        if (platform == UnrealTargetPlatform.Mac)
        {
            return "osx";
        }

        if (platform == UnrealTargetPlatform.Linux)
        {
            return "linux";
        }

        if (platform == UnrealTargetPlatform.LinuxArm64)
        {
            return "linux";
        }

        throw new NotSupportedException($"Unsupported target platform for .NET publish: '{platform}'. " + $"Supported platforms: Win64, Mac, Linux, LinuxArm64.");
    }

    public static string GetArchitectureIdentifier(UnrealArch architecture)
    {
        if (architecture == UnrealArch.X64)
        {
            return "x64";
        }

        if (architecture == UnrealArch.Arm64)
        {
            return "arm64";
        }

        throw new NotSupportedException($"Unsupported target architecture for .NET publish: '{architecture}'. " + $"Supported architectures: X64, Arm64.");
    }
    
    public static string GetTargetArchitecture(this UnrealArch architecture)
    {
        return architecture.ToString();
    }
    
    public static string GetTargetPlatform(this UnrealTargetPlatform platform)
    {
        return platform.ToString();
    }
    
    public static string GetDotNetBuildConfiguration(this UnrealTargetConfiguration configuration)
    {
        if (configuration == UnrealTargetConfiguration.Debug || configuration == UnrealTargetConfiguration.DebugGame)
        {
            return "Debug";
        }

        if (configuration == UnrealTargetConfiguration.Development || configuration == UnrealTargetConfiguration.Test || configuration == UnrealTargetConfiguration.Shipping)
        {
            return "Release";
        }

        throw new ArgumentOutOfRangeException(nameof(configuration), configuration, "Unsupported configuration");
    }
    
    public static void CopyGlobalJson(BuildCommand buildCommand)
    {
        string PluginRootDirectory = buildCommand.GetUnrealSharpRootFolder();
        string ScriptDirectory = buildCommand.GetProjectScriptFolder();
        string SourceGlobalJsonPath = Path.Combine(PluginRootDirectory, "Managed", "global.json");
        string DestinationGlobalJsonPath = Path.Combine(ScriptDirectory, "global.json");
        
        if (!Directory.Exists(ScriptDirectory))
        {
            Directory.CreateDirectory(ScriptDirectory);
        }
        
        File.Copy(SourceGlobalJsonPath, DestinationGlobalJsonPath, overwrite: true);
        AddPluginSdkSearchPath(DestinationGlobalJsonPath, ScriptDirectory, PluginRootDirectory);
    }

    private static void AddPluginSdkSearchPath(string globalJsonPath, string scriptDirectory, string pluginRoot)
    {
        string platform = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        string architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException($"Unsupported .NET SDK host architecture: {RuntimeInformation.ProcessArchitecture}")
        };
        string sdkRoot = Path.Combine(pluginRoot, "ThirdParty", "DotNet", "sdk", $"{platform}-{architecture}");
        string relativeSdkRoot = Path.GetRelativePath(scriptDirectory, sdkRoot).Replace('\\', '/');

        JsonObject document = JsonNode.Parse(File.ReadAllText(globalJsonPath))?.AsObject()
            ?? throw new InvalidDataException($"Invalid global.json at '{globalJsonPath}'.");
        JsonObject sdk = document["sdk"]?.AsObject()
            ?? throw new InvalidDataException($"Missing sdk section in '{globalJsonPath}'.");
        sdk["paths"] = new JsonArray(relativeSdkRoot, "$host$");

        File.WriteAllText(globalJsonPath, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}

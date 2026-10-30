using System.IO;
using UnrealSharp.Automation.Utilities;
using System.Runtime.InteropServices;

namespace UnrealSharp.Automation.Processes;

public class DotnetProcess : BuildToolProcess
{
    public DotnetProcess() : base(DotNetUtilities.DotNetExecutable)
    {
        string LatestDotNetSdkPath = DotNetUtilities.LatestDotNetSdkPath;
        string DotNetRoot = DotNetUtilities.DotnetFolder;
        StartInfo.ArgumentList.Add(Path.Combine(LatestDotNetSdkPath, "dotnet.dll"));
        StartInfo.Environment["DOTNET_ROOT"] = DotNetRoot;
        string architectureVariable = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "DOTNET_ROOT_X64",
            Architecture.Arm64 => "DOTNET_ROOT_ARM64",
            _ => throw new System.PlatformNotSupportedException($"Unsupported .NET host architecture: {RuntimeInformation.ProcessArchitecture}")
        };
        StartInfo.Environment["DOTNET_ROOT_X64"] = null;
        StartInfo.Environment["DOTNET_ROOT_ARM64"] = null;
        StartInfo.Environment[architectureVariable] = DotNetRoot;
        StartInfo.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
        StartInfo.Environment["MSBuildExtensionsPath"] = LatestDotNetSdkPath;
        StartInfo.Environment["MSBUILD_EXE_PATH"] = Path.Combine(LatestDotNetSdkPath, "MSBuild.dll");
        StartInfo.Environment["MSBuildSDKsPath"] = Path.Combine(LatestDotNetSdkPath, "Sdks");
        StartInfo.Environment["DOTNET_ROLL_FORWARD"] = "LatestMinor";
    }
}

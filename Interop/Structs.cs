using System.Runtime.InteropServices;

namespace NssmSharp.Interop;

// Windows服务配置结构体
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct SERVICE_STATUS
{
    public int dwServiceType;
    public int dwCurrentState;
    public int dwControlsAccepted;
    public int dwWin32ExitCode;
    public int dwServiceSpecificExitCode;
    public int dwCheckPoint;
    public int dwWaitHint;
}

// Windows QUERY_SERVICE_CONFIG 结构体
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct QUERY_SERVICE_CONFIG
{
    public int dwServiceType;
    public int dwStartType;
    public int dwErrorControl;
    public IntPtr lpBinaryPathName;
    public IntPtr lpLoadOrderGroup;
    public int dwTagId;
    public IntPtr lpDependencies;
    public IntPtr lpServiceStartName;
    public IntPtr lpDisplayName;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct SERVICE_DESCRIPTION
{
    public string lpDescription;
}

// NSSM服务配置（简化版，后续可扩展）
public class NssmService
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int StartupType { get; set; } = 2; // 默认手动

    // 高级配置
    public string[] Dependencies { get; set; } = [];
    public string EnvironmentVariables { get; set; } = string.Empty; // 格式: key1=val1;key2=val2
    public string StdoutPath { get; set; } = string.Empty;
    public string StderrPath { get; set; } = string.Empty;
    public int Priority { get; set; } = 3; // NORMAL_PRIORITY_CLASS
    public long CpuAffinity { get; set; } // 0=不指定
    public string RecoveryActions { get; set; } = string.Empty; // 格式: restart/ignore/exit
    public bool LogRotation { get; set; }
    public int LogRotationSizeMB { get; set; } = 10;
    public int LogRotationFiles { get; set; } = 5;
}
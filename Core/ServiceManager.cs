using System.Runtime.InteropServices;
using NssmSharp.Interop;
using System.Text.Json;

namespace NssmSharp.Core;
public static class ServiceManager
{
    // 应用服务配置（编辑）
    public static bool ApplyServiceConfig(NssmService config)
    {
        return EditService(config);
    }

    // 读取系统服务配置并转为 NssmService
    public static NssmService? GetNssmServiceConfig(string serviceName)
    {
        var scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_ALL_ACCESS);
        if (scm == IntPtr.Zero) return null;
        var service = IntPtr.Zero;
        try
        {
            service = NativeMethods.OpenService(scm, serviceName, NativeMethods.SERVICE_ALL_ACCESS);
            if (service == IntPtr.Zero)
                return null;
            // 查询主配置
            NativeMethods.QueryServiceConfig(service, IntPtr.Zero, 0, out var bytesNeeded);
            if (bytesNeeded == 0)
                return null;
            var ptr = Marshal.AllocHGlobal(bytesNeeded);
            try
            {
                if (!NativeMethods.QueryServiceConfig(service, ptr, bytesNeeded, out _))
                    return null;
                var qsc = Marshal.PtrToStructure<QUERY_SERVICE_CONFIG>(ptr);
                var config = new NssmService
                {
                    Name = serviceName,
                    DisplayName = Marshal.PtrToStringUni(qsc.lpDisplayName) ?? string.Empty,
                    ExecutablePath = Marshal.PtrToStringUni(qsc.lpBinaryPathName) ?? string.Empty,
                    Arguments = string.Empty, // 需拆分
                    StartupType = qsc.dwStartType,
                    Username = Marshal.PtrToStringUni(qsc.lpServiceStartName) ?? string.Empty,
                    Dependencies = ParseDependencies(qsc.lpDependencies),
                };
                // 解析可执行路径和参数
                if (!string.IsNullOrWhiteSpace(config.ExecutablePath))
                {
                    var exe = config.ExecutablePath.Trim();
                    if (exe.StartsWith('\"'))
                    {
                        var end = exe.IndexOf('"', 1);
                        if (end > 0)
                        {
                            config.ExecutablePath = exe.Substring(1, end - 1);
                            config.Arguments = exe[(end + 1)..].Trim();
                        }
                    }
                    else
                    {
                        var idx = exe.IndexOf(' ');
                        if (idx > 0)
                        {
                            config.ExecutablePath = exe[..idx];
                            config.Arguments = exe[(idx + 1)..].Trim();
                        }
                        else
                        {
                            config.ExecutablePath = exe;
                        }
                    }
                }
                // 读取注册表扩展参数
                var regPath = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath);
                if (key == null) return config;
                config.WorkingDirectory = key.GetValue("WorkingDirectory") as string ?? string.Empty;
                config.EnvironmentVariables = key.GetValue("Environment") as string ?? string.Empty;
                config.StdoutPath = key.GetValue("StdoutPath") as string ?? string.Empty;
                config.StderrPath = key.GetValue("StderrPath") as string ?? string.Empty;
                config.Priority = key.GetValue("ProcessPriority") is int p ? p : 3;
                config.CpuAffinity = key.GetValue("CpuAffinity") is long l ? l : 0;
                config.RecoveryActions = key.GetValue("RecoveryActions") as string ?? string.Empty;
                config.LogRotation = (key.GetValue("LogRotation") is int log && log != 0);
                config.LogRotationSizeMB = key.GetValue("LogRotationSizeMB") is int sz ? sz : 10;
                config.LogRotationFiles = key.GetValue("LogRotationFiles") is int lf ? lf : 5;
                config.Description = key.GetValue("ServiceDescription") as string ?? string.Empty;
                return config;
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
        finally
        {
            if (service != IntPtr.Zero) NativeMethods.CloseServiceHandle(service);
            NativeMethods.CloseServiceHandle(scm);
        }
    }

    // 解析依赖项
    private static string[] ParseDependencies(IntPtr depsPtr)
    {
        if (depsPtr == IntPtr.Zero) return [];
        var deps = Marshal.PtrToStringUni(depsPtr);
        return string.IsNullOrEmpty(deps) ? [] : deps.Split(['\0'], StringSplitOptions.RemoveEmptyEntries);
    }

    // 查询服务状态
    public static (string, int) QueryServiceStatus(string serviceName)
    {
        var scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_ALL_ACCESS);
        if (scm == IntPtr.Zero)
            throw new InvalidOperationException("无法打开服务控制管理器");
        try
        {
            var service = NativeMethods.OpenService(scm, serviceName, NativeMethods.SERVICE_ALL_ACCESS);
            if (service == IntPtr.Zero)
                throw new InvalidOperationException($"未找到服务: {serviceName}");
            var status = new SERVICE_STATUS();
            if (!NativeMethods.QueryServiceStatus(service, ref status))
                throw new InvalidOperationException("无法查询服务状态");
            NativeMethods.CloseServiceHandle(service);
            var stateStr = status.dwCurrentState switch
            {
                1 => "SERVICE_STOPPED",
                2 => "SERVICE_START_PENDING",
                3 => "SERVICE_STOP_PENDING",
                4 => "SERVICE_RUNNING",
                5 => "SERVICE_CONTINUE_PENDING",
                6 => "SERVICE_PAUSE_PENDING",
                7 => "SERVICE_PAUSED",
                _ => $"UNKNOWN({status.dwCurrentState})"
            };
            return (stateStr, status.dwCurrentState);
        }
        finally
        {
            NativeMethods.CloseServiceHandle(scm);
        }
    }

    // 注册表参数读写
    public static object? GetServiceParameter(string serviceName, string param, string? subParam = null)
    {
        var regPath = $@"SYSTEM\\CurrentControlSet\\Services\\{serviceName}";
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath);
        if (key == null) return null;
        if (string.IsNullOrWhiteSpace(subParam))
            return key.GetValue(param);
        // 处理如 AppExit/2 这种子参数
        using var subKey = key.OpenSubKey(param);
        return subKey?.GetValue(subParam);
    }

    public static void SetServiceParameter(string serviceName, string param, object value, string? subParam = null)
    {
        var regPath = $@"SYSTEM\\CurrentControlSet\\Services\\{serviceName}";
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, true);
        if (key == null) throw new InvalidOperationException("服务注册表项不存在");
        if (string.IsNullOrWhiteSpace(subParam))
            key.SetValue(param, value);
        else
        {
            using var subKey = key.CreateSubKey(param);
            subKey.SetValue(subParam, value);
        }
    }

    public static void DeleteServiceParameter(string serviceName, string param, string? subParam = null)
    {
        var regPath = $@"SYSTEM\\CurrentControlSet\\Services\\{serviceName}";
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, true);
        if (key == null) return;
        if (string.IsNullOrWhiteSpace(subParam))
            key.DeleteValue(param, false);
        else
        {
            using var subKey = key.OpenSubKey(param, true);
            subKey?.DeleteValue(subParam, false);
        }
    }

    public static void ResetServiceParameter(string serviceName, string param)
    {
        // 这里简单实现为删除参数
        DeleteServiceParameter(serviceName, param);
    }

    // 触发日志轮转（写入注册表标记）
    public static void TriggerLogRotate(string serviceName)
    {
        var regPath = $@"SYSTEM\\CurrentControlSet\\Services\\{serviceName}";
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, true);
        key?.SetValue("RotateLogNow", 1);
    }

    // 列出所有系统服务
    public static List<string> ListAllServices()
    {
        var result = new List<string>();
        var scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_ALL_ACCESS);
        if (scm == IntPtr.Zero)
            return result;
        try
        {
            var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_Service");
            foreach (var obj in searcher.Get())
            {
                result.Add(obj["Name"]?.ToString() ?? "");
            }
        }
        finally
        {
            NativeMethods.CloseServiceHandle(scm);
        }
        return result;
    }

    // 导出服务配置为 nssm 命令
    public static string DumpServiceAsNssmCmd(string serviceName, string? newName = null)
    {
        var config = ConfigManager.LoadServiceConfig(serviceName);
        if (config == null) throw new InvalidOperationException("未找到服务配置");
        var name = newName ?? config.Name;
        var lines = new List<string>
        {
            $"nssm install {name} \"{config.ExecutablePath}\" {config.Arguments}"
        };
        if (!string.IsNullOrWhiteSpace(config.Description))
            lines.Add($"nssm set {name} Description \"{config.Description}\"");
        if (!string.IsNullOrWhiteSpace(config.WorkingDirectory))
            lines.Add($"nssm set {name} AppDirectory \"{config.WorkingDirectory}\"");
        if (!string.IsNullOrWhiteSpace(config.StdoutPath))
            lines.Add($"nssm set {name} AppStdout \"{config.StdoutPath}\"");
        if (!string.IsNullOrWhiteSpace(config.StderrPath))
            lines.Add($"nssm set {name} AppStderr \"{config.StderrPath}\"");
        // 其他参数可继续补充
        return string.Join("\n", lines);
    }

    public static bool InstallService(NssmService config)
    {
        var scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_ALL_ACCESS);
        if (scm == IntPtr.Zero)
            throw new InvalidOperationException("无法打开服务控制管理器");

        try
        {
            var service = NativeMethods.CreateService(
                scm,
                config.Name,
                config.DisplayName,
                NativeMethods.SERVICE_ALL_ACCESS,
                NativeMethods.SERVICE_WIN32_OWN_PROCESS,
                config.StartupType,
                NativeMethods.SERVICE_ERROR_NORMAL,
                $"\"{config.ExecutablePath}\" {config.Arguments}",
                null,
                IntPtr.Zero,
                config.Dependencies is { Length: > 0 } ? string.Join("\0", config.Dependencies) + "\0\0" : null,
                string.IsNullOrWhiteSpace(config.Username) ? null : config.Username,
                string.IsNullOrWhiteSpace(config.Password) ? null : config.Password
            );
            if (service == IntPtr.Zero)
                throw new InvalidOperationException($"服务创建失败: {config.Name}");

            // 设置描述
            if (!string.IsNullOrWhiteSpace(config.Description))
            {
                SetServiceDescription(service, config.Description);
            }

            // 设置环境变量（写入注册表，服务进程启动时读取）
            if (!string.IsNullOrWhiteSpace(config.EnvironmentVariables))
            {
                try
                {
                    var regPath = $@"SYSTEM\CurrentControlSet\Services\{config.Name}";
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, true);
                    key?.SetValue("Environment", config.EnvironmentVariables, Microsoft.Win32.RegistryValueKind.MultiString);
                }
                catch { /* 忽略异常 */ }
            }

            // 日志轮转、标准流重定向（写入注册表，服务进程启动时实现）
            if (!string.IsNullOrWhiteSpace(config.StdoutPath))
            {
                try
                {
                    var regPath = $@"SYSTEM\CurrentControlSet\Services\{config.Name}";
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, true);
                    key?.SetValue("StdoutPath", config.StdoutPath);
                }
                catch
                {
                    // ignored
                }
            }
            if (!string.IsNullOrWhiteSpace(config.StderrPath))
            {
                try
                {
                    var regPath = $@"SYSTEM\CurrentControlSet\Services\{config.Name}";
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, true);
                    key?.SetValue("StderrPath", config.StderrPath);
                }
                catch
                {
                    // ignored
                }
            }
            if (config.LogRotation)
            {
                try
                {
                    var regPath = $@"SYSTEM\CurrentControlSet\Services\{config.Name}";
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, true);
                    if (key != null)
                    {
                        key.SetValue("LogRotation", 1);
                        key.SetValue("LogRotationSizeMB", config.LogRotationSizeMB);
                        key.SetValue("LogRotationFiles", config.LogRotationFiles);
                    }
                }
                catch
                {
                    // ignored
                }
            }


            // 进程树递归终止（写入注册表，服务进程可读取并实现）
            if (config.Priority != 3) // 3 = NORMAL_PRIORITY_CLASS
            {
                try
                {
                    var regPath = $@"SYSTEM\CurrentControlSet\Services\{config.Name}";
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, true);
                    key?.SetValue("ProcessPriority", config.Priority);
                }
                catch
                {
                    // ignored
                }
            }
            if (config.CpuAffinity != 0)
            {
                try
                {
                    var regPath = $@"SYSTEM\CurrentControlSet\Services\{config.Name}";
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, true);
                    key?.SetValue("CpuAffinity", config.CpuAffinity);
                }
                catch
                {
                    // ignored
                }
            }
            if (!string.IsNullOrWhiteSpace(config.RecoveryActions))
            {
                try
                {
                    var regPath = $@"SYSTEM\CurrentControlSet\Services\{config.Name}";
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, true);
                    key?.SetValue("RecoveryActions", config.RecoveryActions);
                }
                catch
                {
                    // ignored
                }
            }
            // 事件日志与多语言（写入注册表，服务进程可读取并实现）
            if (!string.IsNullOrWhiteSpace(config.Description))
            {
                try
                {
                    var regPath = $@"SYSTEM\CurrentControlSet\Services\{config.Name}";
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, true);
                    key?.SetValue("ServiceDescription", config.Description);
                }
                catch
                {
                    // ignored
                }
            }
            if (!string.IsNullOrWhiteSpace(config.WorkingDirectory))
            {
                try
                {
                    var regPath = $@"SYSTEM\CurrentControlSet\Services\{config.Name}";
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, true);
                    key?.SetValue("WorkingDirectory", config.WorkingDirectory);
                }
                catch
                {
                    // ignored
                }
            }

            NativeMethods.CloseServiceHandle(service);
            return true;
        }
        finally
        {
            NativeMethods.CloseServiceHandle(scm);
        }
    }

    private static bool EditService(NssmService config)
    {
        // 优先尝试用 ChangeServiceConfig 修改服务参数
        var scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_ALL_ACCESS);
        if (scm == IntPtr.Zero)
            throw new InvalidOperationException("无法打开服务控制管理器");
        try
        {
            var service = NativeMethods.OpenService(scm, config.Name, NativeMethods.SERVICE_ALL_ACCESS);
            if (service == IntPtr.Zero)
                throw new InvalidOperationException($"未找到服务: {config.Name}");

            // 修改服务主配置
            var ok = NativeMethods.ChangeServiceConfig(
                service,
                NativeMethods.SERVICE_WIN32_OWN_PROCESS,
                config.StartupType,
                NativeMethods.SERVICE_ERROR_NORMAL,
                string.IsNullOrWhiteSpace(config.ExecutablePath) ? null : ($"\"{config.ExecutablePath}\" {config.Arguments}").Trim(),
                null,
                IntPtr.Zero,
                config.Dependencies is { Length: > 0 } ? string.Join("\0", config.Dependencies) + "\0\0" : null,
                string.IsNullOrWhiteSpace(config.Username) ? null : config.Username,
                string.IsNullOrWhiteSpace(config.Password) ? null : config.Password,
                string.IsNullOrWhiteSpace(config.DisplayName) ? null : config.DisplayName
            );
            if (!ok)
                throw new InvalidOperationException("ChangeServiceConfig 失败");

            // 设置描述
            if (!string.IsNullOrWhiteSpace(config.Description))
            {
                SetServiceDescription(service, config.Description);
            }

            // 其他参数（注册表）
            if (!string.IsNullOrWhiteSpace(config.WorkingDirectory))
            {
                var regPath = $@"SYSTEM\\CurrentControlSet\\Services\\{config.Name}";
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, true);
                key?.SetValue("WorkingDirectory", config.WorkingDirectory);
            }
            if (!string.IsNullOrWhiteSpace(config.StdoutPath))
            {
                var regPath = $@"SYSTEM\\CurrentControlSet\\Services\\{config.Name}";
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, true);
                key?.SetValue("StdoutPath", config.StdoutPath);
            }
            if (!string.IsNullOrWhiteSpace(config.StderrPath))
            {
                var regPath = $@"SYSTEM\\CurrentControlSet\\Services\\{config.Name}";
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, true);
                key?.SetValue("StderrPath", config.StderrPath);
            }
            if (!string.IsNullOrWhiteSpace(config.EnvironmentVariables))
            {
                var regPath = $@"SYSTEM\\CurrentControlSet\\Services\\{config.Name}";
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath, true);
                key?.SetValue("Environment", config.EnvironmentVariables, Microsoft.Win32.RegistryValueKind.MultiString);
            }
            // 日志轮转等参数
            var regPath2 = $@"SYSTEM\\CurrentControlSet\\Services\\{config.Name}";
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath2, true))
            {
                if (key != null)
                {
                    key.SetValue("LogRotation", config.LogRotation ? 1 : 0);
                    key.SetValue("LogRotationSizeMB", config.LogRotationSizeMB);
                    key.SetValue("LogRotationFiles", config.LogRotationFiles);
                    key.SetValue("ProcessPriority", config.Priority);
                    key.SetValue("CpuAffinity", config.CpuAffinity);
                    if (!string.IsNullOrWhiteSpace(config.RecoveryActions))
                        key.SetValue("RecoveryActions", config.RecoveryActions);
                }
            }

            NativeMethods.CloseServiceHandle(service);
            return true;
        }
        finally
        {
            NativeMethods.CloseServiceHandle(scm);
        }
    }

    public static bool ExportService(string serviceName, string exportPath)
    {
        var config = ConfigManager.LoadServiceConfig(serviceName);
        if (config == null) return false;
        File.WriteAllText(exportPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        return true;
    }

    public static bool ImportService(string importPath)
    {
        var config = JsonSerializer.Deserialize<NssmService>(File.ReadAllText(importPath));
        return config != null && InstallService(config);
    }

    public static List<string> ListServices()
    {
        var dir = new DirectoryInfo("configs");
        return !dir.Exists ? [] : dir.GetFiles("*.json").Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToList();
    }

    private static void SetServiceDescription(IntPtr service, string description)
    {
        var info = new SERVICE_DESCRIPTION { lpDescription = description };
        NativeMethods.ChangeServiceConfig2(service, 1, ref info); // 1 = SERVICE_CONFIG_DESCRIPTION
    }

    public static bool UninstallService(string serviceName)
    {
        var scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_ALL_ACCESS);
        if (scm == IntPtr.Zero)
            throw new InvalidOperationException("无法打开服务控制管理器");
        try
        {
            var service = NativeMethods.OpenService(scm, serviceName, NativeMethods.SERVICE_ALL_ACCESS);
            if (service == IntPtr.Zero)
                throw new InvalidOperationException($"未找到服务: {serviceName}");
            var result = NativeMethods.DeleteService(service);
            NativeMethods.CloseServiceHandle(service);
            return result;
        }
        finally
        {
            NativeMethods.CloseServiceHandle(scm);
        }
    }

    public static bool StartService(string serviceName)
    {
        var scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_ALL_ACCESS);
        if (scm == IntPtr.Zero)
            throw new InvalidOperationException("无法打开服务控制管理器");
        try
        {
            var service = NativeMethods.OpenService(scm, serviceName, NativeMethods.SERVICE_ALL_ACCESS);
            if (service == IntPtr.Zero)
                throw new InvalidOperationException($"未找到服务: {serviceName}");
            var result = NativeMethods.StartService(service, 0, null);
            NativeMethods.CloseServiceHandle(service);
            return result;
        }
        finally
        {
            NativeMethods.CloseServiceHandle(scm);
        }
    }

    public static bool StopService(string serviceName)
    {
        var scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_ALL_ACCESS);
        if (scm == IntPtr.Zero)
            throw new InvalidOperationException("无法打开服务控制管理器");
        try
        {
            var service = NativeMethods.OpenService(scm, serviceName, NativeMethods.SERVICE_ALL_ACCESS);
            if (service == IntPtr.Zero)
                throw new InvalidOperationException($"未找到服务: {serviceName}");
            var status = new SERVICE_STATUS();
            var result = NativeMethods.ControlService(service, 1 /* SERVICE_CONTROL_STOP */, ref status);
            NativeMethods.CloseServiceHandle(service);
            return result;
        }
        finally
        {
            NativeMethods.CloseServiceHandle(scm);
        }
    }
}
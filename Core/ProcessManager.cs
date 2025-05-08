using System.Diagnostics;

namespace NssmSharp.Core;

public static class ProcessManager
{
    // 查询服务关联进程（通过服务名查找）
    public static List<(int pid, string exe)> GetServiceProcesses(string serviceName)
    {
        var result = new List<(int, string)>();
        // 这里只简单用 WMI 查询
        var searcher = new System.Management.ManagementObjectSearcher($"SELECT * FROM Win32_Service WHERE Name='{serviceName}'");
        foreach (var obj in searcher.Get())
        {
            var pid = Convert.ToInt32(obj["ProcessId"] ?? 0);
            if (pid <= 0) continue;
            try
            {
                var proc = Process.GetProcessById(pid);
                result.Add((pid, proc.MainModule?.FileName ?? ""));
            }
            catch
            {
                // ignored
            }
        }
        return result;
    }
}
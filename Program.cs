using NssmSharp.Core;

#if WINDOWS
using NssmSharp.Gui;
#endif

namespace NssmSharp;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        switch (args.Length)
        {
#if WINDOWS
            case 1 when args[0].Equals("gui", StringComparison.CurrentCultureIgnoreCase):
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return;
#endif
            case 0:
                PrintUsage();
                return;
            default:
                try
                {
                    var cmd = args[0].ToLower();
                    switch (cmd)
                    {
                        case "install":
                            if (args.Length < 3)
                            {
                                Console.WriteLine("用法: nssmsharp install <服务名> <可执行文件路径> [参数]");
                                return;
                            }
                            var installConfig = new Interop.NssmService
                            {
                                Name = args[1],
                                DisplayName = args[1],
                                ExecutablePath = args[2],
                                Arguments = args.Length > 3 ? string.Join(" ", args[3..]) : string.Empty
                            };
                            ServiceManager.InstallService(installConfig);
                            Console.WriteLine($"服务 {args[1]} 安装成功");
                            break;
                        case "edit":
                            if (args.Length < 2)
                            {
                                Console.WriteLine("用法: nssmsharp edit <服务名>");
                                return;
                            }
#if WINDOWS
                            try
                            {
                                var serviceName = args[1];
                                // 1. 读取当前服务配置
                                var editConfig = ConfigManager.LoadServiceConfig(serviceName);
                                if (editConfig == null)
                                {
                                    // 若无本地配置，尝试从系统读取
                                    editConfig = ServiceManager.GetNssmServiceConfig(serviceName);
                                    if (editConfig == null)
                                    {
                                        Console.WriteLine($"未找到服务 {serviceName} 的配置");
                                        break;
                                    }
                                }
                                // 2. 弹出编辑窗体
                                using var form = new ServiceConfigForm(editConfig);
                                if (form.ShowDialog() == DialogResult.OK && form.ServiceConfig != null)
                                {
                                    // 3. 保存配置并应用
                                    var newConfig = form.ServiceConfig;
                                    var cfgMgr = new ConfigManager();
                                    cfgMgr.SaveServiceConfig(newConfig);
                                    ServiceManager.ApplyServiceConfig(newConfig);
                                    Console.WriteLine($"服务 {serviceName} 配置已更新");
                                }
                                else
                                {
                                    Console.WriteLine("操作已取消，无更改");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"编辑失败: {ex.Message}");
                            }
#else
                            Console.WriteLine("当前平台不支持 edit 交互式编辑");
#endif
                            break;
                        case "remove":
                            switch (args.Length)
                            {
                                case < 2:
                                    Console.WriteLine("用法: nssmsharp remove <服务名> [confirm]");
                                    return;
                                case > 2 when args[2].Equals("confirm", StringComparison.OrdinalIgnoreCase):
                                    ServiceManager.UninstallService(args[1]);
                                    Console.WriteLine($"服务 {args[1]} 已卸载");
                                    break;
                                default:
                                {
                                    Console.Write($"确认要卸载服务 {args[1]}? (y/n): ");
                                    var key = Console.ReadKey();
                                    Console.WriteLine();
                                    if (key.KeyChar is 'y' or 'Y')
                                    {
                                        ServiceManager.UninstallService(args[1]);
                                        Console.WriteLine($"服务 {args[1]} 已卸载");
                                    }
                                    else
                                    {
                                        Console.WriteLine("操作已取消");
                                    }
                                    break;
                                }
                            }
                            break;
                        case "start":
                            if (args.Length < 2)
                            {
                                Console.WriteLine("用法: nssmsharp start <服务名>");
                                return;
                            }
                            ServiceManager.StartService(args[1]);
                            Console.WriteLine($"服务 {args[1]} 启动成功");
                            break;
                        case "stop":
                            if (args.Length < 2)
                            {
                                Console.WriteLine("用法: nssmsharp stop <服务名>");
                                return;
                            }
                            ServiceManager.StopService(args[1]);
                            Console.WriteLine($"服务 {args[1]} 停止成功");
                            break;
                        case "restart":
                            if (args.Length < 2)
                            {
                                Console.WriteLine("用法: nssmsharp restart <服务名>");
                                return;
                            }
                            ServiceManager.StopService(args[1]);
                            ServiceManager.StartService(args[1]);
                            Console.WriteLine($"服务 {args[1]} 重启成功");
                            break;
                        case "status":
                            if (args.Length < 2)
                            {
                                Console.WriteLine("用法: nssmsharp status <服务名>");
                                return;
                            }
                            try
                            {
                                var (state, _) = ServiceManager.QueryServiceStatus(args[1]);
                                Console.WriteLine($"服务 {args[1]} 状态: {state}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"查询失败: {ex.Message}");
                            }
                            break;
                        case "statuscode":
                            if (args.Length < 2)
                            {
                                Console.WriteLine("用法: nssmsharp statuscode <服务名>");
                                return;
                            }
                            try
                            {
                                var (_, code) = ServiceManager.QueryServiceStatus(args[1]);
                                Console.WriteLine(code);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"查询失败: {ex.Message}");
                            }
                            break;
                        case "rotate":
                            if (args.Length < 2)
                            {
                                Console.WriteLine("用法: nssmsharp rotate <服务名>");
                                return;
                            }
                            try
                            {
                                ServiceManager.TriggerLogRotate(args[1]);
                                Console.WriteLine($"已请求服务 {args[1]} 日志轮转");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"操作失败: {ex.Message}");
                            }
                            break;
                        case "list":
                            if (args.Length > 1 && args[1].Equals("all", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    var all = ServiceManager.ListAllServices();
                                    Console.WriteLine("系统所有服务:");
                                    foreach (var s in all)
                                        Console.WriteLine($"  {s}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"查询失败: {ex.Message}");
                                }
                            }
                            else
                            {
                                var list = ServiceManager.ListServices();
                                Console.WriteLine("NssmSharp 管理的服务:");
                                foreach (var s in list)
                                    Console.WriteLine($"  {s}");
                            }
                            break;
                        case "processes":
                            if (args.Length < 2)
                            {
                                Console.WriteLine("用法: nssmsharp processes <服务名>");
                                return;
                            }
                            try
                            {
                                var procs = ProcessManager.GetServiceProcesses(args[1]);
                                if (procs.Count == 0)
                                {
                                    Console.WriteLine("未找到关联进程");
                                }
                                else
                                {
                                    foreach (var (pid, exe) in procs)
                                        Console.WriteLine($"PID={pid} {exe}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"查询失败: {ex.Message}");
                            }
                            break;
                        case "dump":
                            if (args.Length < 2)
                            {
                                Console.WriteLine("用法: nssmsharp dump <服务名> [新服务名]");
                                return;
                            }
                            try
                            {
                                var dump = ServiceManager.DumpServiceAsNssmCmd(args[1], args.Length > 2 ? args[2] : null);
                                Console.WriteLine(dump);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"导出失败: {ex.Message}");
                            }
                            break;
                        case "get":
                            if (args.Length < 3)
                            {
                                Console.WriteLine("用法: nssmsharp get <服务名> <参数名> [子参数]");
                                return;
                            }
                            try
                            {
                                var val = ServiceManager.GetServiceParameter(args[1], args[2], args.Length > 3 ? args[3] : null);
                                Console.WriteLine(val ?? "(无)");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"查询失败: {ex.Message}");
                            }
                            break;
                        case "set":
                            if (args.Length < 4)
                            {
                                Console.WriteLine("用法: nssmsharp set <服务名> <参数名> <值...>");
                                return;
                            }
                            try
                            {
                                var value = string.Join(" ", args[3..]);
                                ServiceManager.SetServiceParameter(args[1], args[2], value);
                                Console.WriteLine("设置成功");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"设置失败: {ex.Message}");
                            }
                            break;
                        case "reset":
                            if (args.Length < 3)
                            {
                                Console.WriteLine("用法: nssmsharp reset <服务名> <参数名>");
                                return;
                            }
                            try
                            {
                                ServiceManager.ResetServiceParameter(args[1], args[2]);
                                Console.WriteLine("已重置");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"操作失败: {ex.Message}");
                            }
                            break;
                        case "unset":
                            if (args.Length < 3)
                            {
                                Console.WriteLine("用法: nssmsharp unset <服务名> <参数名>");
                                return;
                            }
                            try
                            {
                                ServiceManager.DeleteServiceParameter(args[1], args[2]);
                                Console.WriteLine("已删除");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"操作失败: {ex.Message}");
                            }
                            break;
                        case "gui":
#if WINDOWS
                            Application.EnableVisualStyles();
                            Application.SetCompatibleTextRenderingDefault(false);
                            Application.Run(new MainForm());
#else
                            Console.WriteLine("当前平台不支持 GUI");
#endif
                            break;
                        default:
                            PrintUsage();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"操作失败: {ex.Message}");
                }
                break;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
                          NssmSharp - 纯C# NSSM服务管理器\n\n用法:
                            nssmsharp install <服务名> <可执行文件路径> [参数]
                            nssmsharp edit <服务名>
                            nssmsharp remove <服务名> [confirm]
                            nssmsharp start <服务名>
                            nssmsharp stop <服务名>
                            nssmsharp restart <服务名>
                            nssmsharp status <服务名>
                            nssmsharp statuscode <服务名>
                            nssmsharp rotate <服务名>
                            nssmsharp list [all]
                            nssmsharp processes <服务名>
                            nssmsharp dump <服务名> [新服务名]
                            nssmsharp get <服务名> <参数名> [子参数]
                            nssmsharp set <服务名> <参数名> <值...>
                            nssmsharp reset <服务名> <参数名>
                            nssmsharp unset <服务名> <参数名>
                            nssmsharp gui

                          参数说明和详细文档请参考 nssm 原版文档和 nssmsharp README.md。
                          """);
    }
}
using System;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Win32.TaskScheduler;
using Sumo.Core;

namespace SumoController
{
    class Program
    {
        static async System.Threading.Tasks.Task Main(string[] args)
        {
            Console.WriteLine("SumoController Initializing...");

            // 1. 检查管理员权限，如果没有则申请提权重启
            if (!IsAdministrator())
            {
                Console.WriteLine("Need Administrator privileges. Elevating...");
                ElevateAndRestart();
                return; // 提权后当前进程退出
            }

            Console.WriteLine("Running with Administrator privileges.");

            // 2. 检查并注册开机自启任务计划
            EnsureAutoStartTask();

            // 3. 下载更新配置和组件
            var config = await UpdateManager.SyncConfigAndPluginsAsync();
            
            // 4. 通过反射加载并启动 Plugins
            PluginManager.LoadAndStartPlugins(config);

            Console.WriteLine("SumoController started. Press Ctrl+C to exit.");
            
            // 捕获 Ctrl+C 以便优雅退出并停止插件
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // 阻止立即退出
                PluginManager.StopAll();
                Environment.Exit(0);
            };

            // 保持主线程运行
            await System.Threading.Tasks.Task.Delay(-1);
        }

        private static bool IsAdministrator()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static void ElevateAndRestart()
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;

            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = exePath,
                Verb = "runas" // 请求管理员权限
            };

            try
            {
                Process.Start(startInfo);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Console.WriteLine("UAC Elevation was cancelled by the user.");
            }
        }

        private static void EnsureAutoStartTask()
        {
            const string taskName = "SumoControllerAutoStart";
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;

            if (string.IsNullOrEmpty(exePath)) return;

            if (!IsAdministrator())
            {
                Console.WriteLine("Insufficient privileges to create Task Scheduler task. Administrator rights are required.");
                return;
            }

            try
            {
                using (TaskService ts = new TaskService())
                {
                    if (!ts.Connected)
                    {
                        Console.WriteLine("Task Scheduler service is not available.");
                        return;
                    }

                    // 检查是否已经存在
                    if (ts.RootFolder.Tasks.Exists(taskName))
                    {
                        Console.WriteLine($"Task '{taskName}' already exists.");
                        return;
                    }

                    // 创建新任务
                    TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = "Starts SumoController with highest privileges at system startup.";
                    
                    // 以最高权限运行
                    td.Principal.RunLevel = TaskRunLevel.Highest;

                    // 触发器：开机启动 (可以根据需求改为用户登录时 LogonTrigger)
                    td.Triggers.Add(new BootTrigger());

                    // 动作：运行主程序
                    td.Actions.Add(new ExecAction(exePath, null, System.IO.Path.GetDirectoryName(exePath)));

                    // 注册任务
                    ts.RootFolder.RegisterTaskDefinition(taskName, td);
                    Console.WriteLine($"Successfully registered Task '{taskName}'.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create Task Scheduler task: {ex.Message}");
            }
        }
    }
}

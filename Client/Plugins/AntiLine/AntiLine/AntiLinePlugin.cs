using System;
using System.Collections.Generic;
using System.Diagnostics;
using Sumo.Core;

namespace AntiLine
{
    public class AntiLinePlugin : IPlugin
    {
        public string Name => "AntiLine";
        public string Version => "1.0.0";
        public string Description => "Disables wired ethernet network connections.";

        public void Initialize(string serverUrl, Dictionary<string, object>? config)
        {
            // 无需配置
        }

        public void Start()
        {
            Console.WriteLine("[AntiLine] Attempting to disable Wired Ethernet adapters...");
            
            // 使用 PowerShell 禁用有线网卡 (MediaType 等于 '802.3' 通常指以太网)
            string script = @"
                $ethernetAdapters = Get-NetAdapter -Physical | Where-Object { $_.MediaType -match '802.3' -or $_.Name -match 'Ethernet|以太网' }
                foreach ($adapter in $ethernetAdapters) {
                    if ($adapter.Status -ne 'Disabled') {
                        Disable-NetAdapter -Name $adapter.Name -Confirm:$false -ErrorAction SilentlyContinue
                    }
                }
            ";

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    process.WaitForExit(5000); // 最多等 5 秒
                    Console.WriteLine("[AntiLine] Ethernet disable script executed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AntiLine] Failed to disable Ethernet: {ex.Message}");
            }
        }

        public void Stop()
        {
            Console.WriteLine("[AntiLine] Stopped.");
        }
    }
}

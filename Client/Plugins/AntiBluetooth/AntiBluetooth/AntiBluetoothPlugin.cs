using System;
using System.Collections.Generic;
using System.Diagnostics;
using Sumo.Core;

namespace AntiBluetooth
{
    public class AntiBluetoothPlugin : IPlugin
    {
        public string Name => "AntiBluetooth";
        public string Version => "1.0.0";
        public string Description => "Disables Bluetooth adapters on the system.";

        public void Initialize(string serverUrl, Dictionary<string, object>? config)
        {
            // 无需配置
        }

        public void Start()
        {
            Console.WriteLine("[AntiBluetooth] Attempting to disable Bluetooth devices...");
            
            // 使用 PowerShell 和 WMI 查找并禁用含有 "Bluetooth" 或 "蓝牙" 的 PNP 设备
            string script = @"
                $btDevices = Get-PnpDevice -Class Bluetooth -ErrorAction SilentlyContinue
                foreach ($dev in $btDevices) {
                    if ($dev.Status -eq 'OK') {
                        Disable-PnpDevice -InstanceId $dev.InstanceId -Confirm:$false -ErrorAction SilentlyContinue
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
                    Console.WriteLine("[AntiBluetooth] Bluetooth disable script executed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AntiBluetooth] Failed to disable Bluetooth: {ex.Message}");
            }
        }

        public void Stop()
        {
            // 不做任何恢复操作，保持禁用状态
            Console.WriteLine("[AntiBluetooth] Stopped.");
        }
    }
}

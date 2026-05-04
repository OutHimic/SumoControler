using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sumo.Core;

namespace BoxLogger
{
    public class BoxLoggerPlugin : IPlugin
    {
        public string Name => "BoxLogger";
        public string Version => "1.0.0";
        public string Description => "Logs opened window titles and process paths and uploads them to the server.";

        private string _serverUrl = "http://127.0.0.1:8000";
        private int _intervalMs = 1000;
        private CancellationTokenSource? _cts;
        private Task? _monitorTask;
        private static readonly HttpClient _httpClient = new HttpClient();

        private IntPtr _lastWindowHandle = IntPtr.Zero;

        // === Win32 API ===
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        public void Initialize(string serverUrl, Dictionary<string, object>? config)
        {
            if (!string.IsNullOrEmpty(serverUrl))
            {
                _serverUrl = serverUrl.TrimEnd('/');
            }

            if (config != null && config.TryGetValue("upload_interval_seconds", out object? intervalObj))
            {
                if (int.TryParse(intervalObj?.ToString(), out int seconds) && seconds > 0)
                {
                    _intervalMs = seconds * 1000;
                }
            }
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorLoop(_cts.Token), _cts.Token);
            Console.WriteLine($"[BoxLogger] Started. Polling interval: {_intervalMs}ms. Server: {_serverUrl}");
        }

        public void Stop()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_monitorTask != null && !_monitorTask.IsCompleted)
            {
                try { _monitorTask.Wait(2000); } catch { }
            }
            Console.WriteLine("[BoxLogger] Stopped.");
        }

        private async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    IntPtr currentWindow = GetForegroundWindow();

                    // 只有当焦点窗口改变时才记录
                    if (currentWindow != IntPtr.Zero && currentWindow != _lastWindowHandle)
                    {
                        _lastWindowHandle = currentWindow;
                        LogAndUpload(currentWindow);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BoxLogger] Error: {ex.Message}");
                }

                await Task.Delay(_intervalMs, token);
            }
        }

        private void LogAndUpload(IntPtr hWnd)
        {
            string title = GetWindowTitle(hWnd);
            string processPath = GetProcessPath(hWnd);

            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(processPath)) return;

            var logEntry = new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                window_title = title,
                process_path = processPath
            };

            // 异步触发上传，不阻塞监控线程
            _ = Task.Run(async () =>
            {
                try
                {
                    string json = JsonSerializer.Serialize(logEntry);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync($"{_serverUrl}/api/logs", content);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[BoxLogger] Failed to upload log. Status: {response.StatusCode}");
                    }
                    else
                    {
                        Console.WriteLine($"[BoxLogger] Uploaded: {title} | {processPath}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BoxLogger] Upload exception: {ex.Message}");
                }
            });
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            if (GetWindowText(hWnd, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return string.Empty;
        }

        private string GetProcessPath(IntPtr hWnd)
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0) return string.Empty;

            IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess == IntPtr.Zero) return string.Empty;

            try
            {
                uint capacity = 1024;
                StringBuilder sb = new StringBuilder((int)capacity);
                if (QueryFullProcessImageName(hProcess, 0, sb, ref capacity))
                {
                    return sb.ToString();
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }

            return string.Empty;
        }
    }
}

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sumo.Core;

namespace AntiEdgeSurf
{
    public class AntiEdgeSurfPlugin : IPlugin
    {
        public string Name => "AntiEdgeSurf";
        public string Version => "1.0.0";
        public string Description => "Automatically detects and closes Edge Surf game windows.";

        private CancellationTokenSource? _cts;
        private Task? _monitorTask;

        // === Win32 API Definitions ===
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;

        public void Initialize(string serverUrl, System.Collections.Generic.Dictionary<string, object>? config)
        {
            // 本组件不需要配置
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorLoop(_cts.Token), _cts.Token);
            Console.WriteLine("[AntiEdgeSurf] Started monitoring for edge://surf.");
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
                try
                {
                    _monitorTask.Wait(2000); // 留2秒优雅退出
                }
                catch (AggregateException) { }
            }
            Console.WriteLine("[AntiEdgeSurf] Stopped.");
        }

        private async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    EnumWindows(CheckAndCloseSurfWindow, IntPtr.Zero);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AntiEdgeSurf] Error during window enumeration: {ex.Message}");
                }

                // 避免高频轮询占用 CPU，1.5 秒扫一次足以
                await Task.Delay(1500, token);
            }
        }

        private bool CheckAndCloseSurfWindow(IntPtr hWnd, IntPtr lParam)
        {
            int length = GetWindowTextLength(hWnd);
            if (length == 0) return true;

            StringBuilder sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();

            // 统一转换为小写，并将反斜杠替换为正斜杠以便匹配
            string normalizedTitle = title.ToLower().Replace("\\", "/");

            if (normalizedTitle.Contains("edge://surf"))
            {
                Console.WriteLine($"[AntiEdgeSurf] Found Edge Surf window! Title: '{title}'. Closing it...");
                PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }

            return true; // 继续枚举下一个窗口
        }
    }
}

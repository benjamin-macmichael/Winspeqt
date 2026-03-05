using Microsoft.UI.Xaml;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Winspeqt.Services;

namespace Winspeqt.Helpers
{
    public partial class SystemTrayHelper : IDisposable
    {
        private AppUsageService _appUsageService;
        private Window _mainWindow;
        private IntPtr _hwnd;

        private const int WM_APP = 0x8000;
        private const int WM_TRAYICON = WM_APP + 1;
        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;
        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_DELETE = 0x00000002;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint MF_STRING = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_RIGHTBUTTON = 0x0002;

        private const int IDM_OPEN = 1001;
        private const int IDM_EXIT = 1003;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private Microsoft.UI.Dispatching.DispatcherQueueTimer? _messageTimer;

        public SystemTrayHelper(Window mainWindow, AppUsageService appUsageService)
        {
            _mainWindow = mainWindow;
            _appUsageService = appUsageService;
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);

            InitializeTrayIcon();
            StartMessagePolling();
        }

        private void StartMessagePolling()
        {
            _messageTimer = _mainWindow.DispatcherQueue.CreateTimer();
            _messageTimer.Interval = TimeSpan.FromMilliseconds(100);
            _messageTimer.Tick += (s, e) => CheckTrayMessages();
            _messageTimer.Start();
        }

        private void CheckTrayMessages()
        {
            // Poll for messages - simplified approach
        }

        private void InitializeTrayIcon()
        {
            IntPtr hIcon = IntPtr.Zero;

            string[] possiblePaths = new[]
            {
                System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Quantum Lens Transparent (Icon).ico"),
            };

            foreach (var iconPath in possiblePaths)
            {
                if (System.IO.File.Exists(iconPath))
                {
                    try
                    {
                        var icon = new Icon(iconPath);
                        hIcon = icon.Handle;
                        break;
                    }
                    catch { }
                }
            }

            if (hIcon == IntPtr.Zero)
            {
                hIcon = ExtractIcon(GetModuleHandle(null), "shell32.dll", 15);
            }

            var nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = hIcon,
                szTip = "Winspeqt - Right-click for menu"
            };

            Shell_NotifyIcon(NIM_ADD, ref nid);

            HookWindowProc();
        }

        private WndProcDelegate? _wndProcDelegate;
        private IntPtr _oldWndProc;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const int GWLP_WNDPROC = -4;

        private void HookWindowProc()
        {
            _wndProcDelegate = new WndProcDelegate(WndProc);
            IntPtr funcPtr = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);

            if (IntPtr.Size == 8)
                _oldWndProc = SetWindowLongPtr64(_hwnd, GWLP_WNDPROC, funcPtr);
            else
                _oldWndProc = SetWindowLong32(_hwnd, GWLP_WNDPROC, funcPtr);
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_TRAYICON)
            {
                int lParamInt = lParam.ToInt32();

                if (lParamInt == WM_LBUTTONDBLCLK)
                {
                    _mainWindow.DispatcherQueue.TryEnqueue(() => ShowMainWindow());
                    return IntPtr.Zero;
                }
                else if (lParamInt == WM_RBUTTONUP)
                {
                    _mainWindow.DispatcherQueue.TryEnqueue(() => ShowContextMenu());
                    return IntPtr.Zero;
                }
            }

            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        private void ShowMainWindow()
        {
            try
            {
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                appWindow.Show();

                var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                if (presenter != null && presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Minimized)
                {
                    presenter.Restore();
                }

                _mainWindow.Activate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing window: {ex.Message}");
            }
        }

        private void ShowContextMenu()
        {
            try
            {
                IntPtr hMenu = CreatePopupMenu();

                InsertMenu(hMenu, 0, MF_STRING, IDM_OPEN, "Open Winspeqt");
                InsertMenu(hMenu, 1, MF_SEPARATOR, UIntPtr.Zero, null);
                InsertMenu(hMenu, 2, MF_STRING, IDM_EXIT, "Exit");

                POINT pt;
                GetCursorPos(out pt);
                SetForegroundWindow(_hwnd);

                int cmd = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_RIGHTBUTTON, pt.X, pt.Y, _hwnd, IntPtr.Zero);

                if (cmd == IDM_OPEN)
                {
                    ShowMainWindow();
                }
                else if (cmd == IDM_EXIT)
                {
                    ExitApplication();
                }

                DestroyMenu(hMenu);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing menu: {ex.Message}");
            }
        }

        private void ExitApplication()
        {
            _appUsageService.SaveData();
            _appUsageService.Dispose();

            var nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwnd,
                uID = 1
            };
            Shell_NotifyIcon(NIM_DELETE, ref nid);

            _mainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                if (_mainWindow is Views.MainWindow mainWin)
                {
                    mainWin.CleanupAndExit();
                }
                Microsoft.UI.Xaml.Application.Current.Exit();
            });
        }

        public void HideToTray()
        {
            // Window hidden
        }

        public void Dispose()
        {
            _messageTimer?.Stop();

            var nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwnd,
                uID = 1
            };
            Shell_NotifyIcon(NIM_DELETE, ref nid);
        }
    }
}
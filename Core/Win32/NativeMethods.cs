using System;
using System.Runtime.InteropServices;

namespace GlowBorder.Core.Win32
{
    public static class NativeMethods
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TOPMOST = 0x00000008;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            else
                return new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, (int)dwNewLong.ToInt64()));
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        public const uint HWND_BROADCAST = 0xffff;
        public static readonly uint WM_SHOW_GLOWBORDER = RegisterWindowMessage("WM_SHOW_GLOWBORDER_NOTIGLOW");

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public const int SW_RESTORE = 9;

        public static void MakeWindowClickThroughAndNoActivate(IntPtr hwnd)
        {
            IntPtr exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            long newStyle = exStyle.ToInt64() | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(newStyle));

            // Force topmost without taking focus
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, uint action, IntPtr pChangeFilterStruct);

        public const uint MSGFLT_ALLOW = 1;

        public delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);

        public static void AllowMessageInUIPI(IntPtr hwnd, uint msg)
        {
            try
            {
                ChangeWindowMessageFilterEx(hwnd, msg, MSGFLT_ALLOW, IntPtr.Zero);
            }
            catch { }
        }

        public static void SignalExistingInstance()
        {
            try
            {
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);

                foreach (var p in processes)
                {
                    if (p.Id != currentProcess.Id)
                    {
                        AllowSetForegroundWindow(p.Id);
                        IntPtr hwnd = p.MainWindowHandle;

                        if (hwnd != IntPtr.Zero)
                        {
                            ShowWindow(hwnd, SW_RESTORE);
                            SetForegroundWindow(hwnd);
                            PostMessage(hwnd, WM_SHOW_GLOWBORDER, IntPtr.Zero, IntPtr.Zero);
                        }

                        // Also post to all thread windows of the target process
                        try
                        {
                            foreach (System.Diagnostics.ProcessThread thread in p.Threads)
                            {
                                EnumThreadWindows((uint)thread.Id, (h, l) =>
                                {
                                    PostMessage(h, WM_SHOW_GLOWBORDER, IntPtr.Zero, IntPtr.Zero);
                                    return true;
                                }, IntPtr.Zero);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            // Broadcast fallback
            PostMessage((IntPtr)HWND_BROADCAST, WM_SHOW_GLOWBORDER, IntPtr.Zero, IntPtr.Zero);
        }
    }
}

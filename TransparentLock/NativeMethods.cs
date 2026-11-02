using System.Runtime.InteropServices;
using System;

namespace TransparentLock
{
    public static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool LockWorkStation();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const int ASFW_ANY = -1;

        [DllImport("user32.dll")]
        public static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        static extern uint SetThreadExecutionState(uint esFlags);

        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;

        public static void PreventSleep()
        {
            SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED);
        }

        public static void AllowSleep()
        {
            SetThreadExecutionState(ES_CONTINUOUS);
        }

        public static int GetIdleTimeInSeconds()
        {
            var info = new LASTINPUTINFO
            {
                cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
            };

            if (!GetLastInputInfo(ref info))
                return 0;

            uint tickCount = (uint)Environment.TickCount;
            uint idleTicks = tickCount - info.dwTime;

            return (int)(idleTicks / 1000);
        }

        // ----- Window z-order / foreground -----

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOOWNERZORDER = 0x0200;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        /// <summary>
        /// Moves the window to the top of the topmost band and pins it to the given pixel bounds.
        /// Cheap enough to be called periodically.
        /// </summary>
        public static void KeepTopmost(IntPtr hwnd, System.Drawing.Rectangle bounds)
        {
            if (hwnd == IntPtr.Zero)
                return;

            uint flags = SWP_NOACTIVATE | SWP_NOOWNERZORDER;

            if (GetWindowRect(hwnd, out var r) &&
                r.Left == bounds.Left && r.Top == bounds.Top &&
                r.Right == bounds.Right && r.Bottom == bounds.Bottom)
            {
                flags |= SWP_NOMOVE | SWP_NOSIZE;
            }

            SetWindowPos(hwnd, HWND_TOPMOST, bounds.X, bounds.Y, bounds.Width, bounds.Height, flags);
        }

        public static bool IsForegroundInCurrentProcess()
        {
            var fg = GetForegroundWindow();
            if (fg == IntPtr.Zero)
                return false;

            GetWindowThreadProcessId(fg, out uint pid);
            return pid == GetCurrentProcessId();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        /// <summary>True if the topmost window at the given screen pixel belongs to this process.</summary>
        public static bool IsWindowAtPointInCurrentProcess(int x, int y)
        {
            var hwnd = WindowFromPoint(new POINT { X = x, Y = y });
            if (hwnd == IntPtr.Zero)
                return false;

            GetWindowThreadProcessId(hwnd, out uint pid);
            return pid == GetCurrentProcessId();
        }

        /// <summary>
        /// Takes the foreground back from another application. SetForegroundWindow alone is
        /// refused by Windows when another process owns the foreground, so we temporarily
        /// attach to the input queue of the current foreground thread.
        /// </summary>
        public static void ForceForeground(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return;

            var fg = GetForegroundWindow();
            if (fg == hwnd)
                return;

            uint currentThread = GetCurrentThreadId();
            uint fgThread = fg == IntPtr.Zero ? 0 : GetWindowThreadProcessId(fg, out _);
            bool attached = fgThread != 0 && fgThread != currentThread &&
                            AttachThreadInput(currentThread, fgThread, true);

            try
            {
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
            }
            finally
            {
                if (attached)
                    AttachThreadInput(currentThread, fgThread, false);
            }
        }

        // ----- Session state -----

        private const uint DESKTOP_SWITCHDESKTOP = 0x0100;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll")]
        private static extern bool CloseDesktop(IntPtr hDesktop);

        /// <summary>
        /// True while the Windows lock screen (or another secure desktop) is shown,
        /// or while this session is disconnected (fast user switching, RDP).
        /// </summary>
        public static bool IsWorkstationLocked()
        {
            var desktop = OpenInputDesktop(0, false, DESKTOP_SWITCHDESKTOP);
            if (desktop == IntPtr.Zero)
                return true;

            CloseDesktop(desktop);
            return false;
        }

        // ----- DWM -----

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_CAPTION_COLOR = 35;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>
        /// Light or dark title bar (Windows 10 20H1+) with a caption color matching the window (Windows 11).
        /// </summary>
        public static void ApplyTitleBarTheme(IntPtr hwnd, bool dark, int captionColorBgr)
        {
            if (hwnd == IntPtr.Zero)
                return;

            try
            {
                int value = dark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref value, sizeof(int));

                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColorBgr, sizeof(int));
            }
            catch (DllNotFoundException)
            {
                // No DWM -> keep the default title bar
            }
        }
    }
}

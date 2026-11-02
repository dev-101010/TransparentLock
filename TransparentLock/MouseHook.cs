using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TransparentLock
{
    /// <summary>
    /// Returns true to swallow the mouse button / wheel event.
    /// </summary>
    internal delegate bool MouseFilter(int x, int y, bool isButtonDown, bool isInjected);

    /// <summary>
    /// Low-level mouse hook. Only button and wheel events are passed to the filter;
    /// mouse moves are never blocked (and cost nothing beyond the type check).
    /// </summary>
    internal sealed class MouseHook : IDisposable
    {
        private const int WH_MOUSE_LL = 14;

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;
        private const int WM_MOUSEHWHEEL = 0x020E;

        private const int LLMHF_INJECTED = 0x00000001;

        private readonly LowLevelMouseProc _proc;
        private readonly MouseFilter _filter;
        private IntPtr _hookId;

        public MouseHook(MouseFilter filter)
        {
            _filter = filter;
            _proc = HookCallback;
            _hookId = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
            if (_hookId == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to install mouse hook.");
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                bool isDown = msg is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN;
                bool isOther = msg is WM_LBUTTONUP or WM_RBUTTONUP or WM_MBUTTONUP or WM_XBUTTONUP
                    or WM_MOUSEWHEEL or WM_MOUSEHWHEEL;

                if (isDown || isOther)
                {
                    var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    bool injected = (data.flags & LLMHF_INJECTED) != 0;

                    try
                    {
                        if (_filter(data.x, data.y, isDown, injected))
                            return new IntPtr(1);
                    }
                    catch (Exception ex)
                    {
                        // Never let an exception escape into the hook chain
                        Debug.WriteLine(ex);
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public int x;
            public int y;
            public uint mouseData;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);
    }
}

using System;
using System.Runtime.InteropServices;

namespace DynamicIsland.Helpers
{
    public static class NativeMethods
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_LAYERED = 0x00080000;

        public const int WM_HOTKEY = 0x0312;
        public const int WM_CLIPBOARDUPDATE = 0x031D;
        public const int WM_DEVICECHANGE = 0x0219;
        public const int DBT_DEVICEARRIVAL = 0x8000;
        public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

        public const int VK_CAPITAL = 0x14;
        public const int VK_NUMLOCK = 0x90;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_NOREPEAT = 0x4000;
        public const uint VK_I = 0x49;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool LockWorkStation();

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

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            return new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_FRAMECHANGED = 0x0020;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public static void SetToolWindowAndTopmost(IntPtr hWnd)
        {
            try
            {
                IntPtr exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE);
                long style = exStyle.ToInt64();
                style |= WS_EX_TOOLWINDOW;
                style |= WS_EX_TOPMOST;
                SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(style));
                SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
            }
            catch
            {
            }
        }

        public static void SetClickThrough(IntPtr hWnd, bool enable)
        {
            IntPtr exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE);
            long style = exStyle.ToInt64();
            if (enable)
            {
                style |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
            }
            else
            {
                style &= ~WS_EX_TRANSPARENT;
            }
            SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(style));
        }

        // ================= WINDOWS 11 DWM ACRYLIC / BLUR =================

        [StructLayout(LayoutKind.Sequential)]
        public struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        public enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_ENABLE_HOSTBACKDROP = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        public enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        [DllImport("user32.dll")]
        public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        public static void EnableAcrylic(IntPtr hWnd, uint gradientColor = 0x750E0C0C)
        {
            try
            {
                int darkMode = 1;
                DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

                int backdropType = 3; // DWMSBT_TRANSIENTWINDOW (Acrylic)
                DwmSetWindowAttribute(hWnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
            }
            catch { }

            try
            {
                var policy = new AccentPolicy
                {
                    AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                    AccentFlags = 2,
                    GradientColor = (int)gradientColor,
                    AnimationId = 0
                };

                int size = Marshal.SizeOf(policy);
                IntPtr pPolicy = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(policy, pPolicy, false);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    Data = pPolicy,
                    SizeOfData = size
                };

                SetWindowCompositionAttribute(hWnd, ref data);
                Marshal.FreeHGlobal(pPolicy);
            }
            catch { }
        }
    }
}

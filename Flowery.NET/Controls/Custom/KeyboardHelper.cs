using System;
using System.Runtime.InteropServices;

namespace Flowery.Controls.Custom
{
    public static class KeyboardHelper
    {
        public static bool IsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsMacOS { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        public static bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static bool HasNativeSupport => IsWindows || IsMacOS;

        public static bool IsCapsLockOn
        {
            get
            {
                if (IsWindows) return WindowsNative.IsKeyToggled(WindowsNative.VK_CAPITAL);
                if (IsMacOS) return MacNative.IsCapsLockOn;
                return false;
            }
        }

        public static bool IsNumLockOn
        {
            get
            {
                if (IsWindows) return WindowsNative.IsKeyToggled(WindowsNative.VK_NUMLOCK);
                return false; // macOS doesn't have Num Lock in the traditional sense
            }
        }

        public static bool IsScrollLockOn
        {
            get
            {
                if (IsWindows) return WindowsNative.IsKeyToggled(WindowsNative.VK_SCROLL);
                return false; // macOS doesn't have Scroll Lock
            }
        }

        public static bool IsShiftPressed
        {
            get
            {
                if (IsWindows) return WindowsNative.IsKeyPressed(WindowsNative.VK_SHIFT);
                if (IsMacOS) return MacNative.IsModifierPressed(MacNative.kCGEventFlagMaskShift);
                return false;
            }
        }

        public static bool IsCtrlPressed
        {
            get
            {
                if (IsWindows) return WindowsNative.IsKeyPressed(WindowsNative.VK_CONTROL);
                if (IsMacOS) return MacNative.IsModifierPressed(MacNative.kCGEventFlagMaskControl);
                return false;
            }
        }

        public static bool IsAltPressed
        {
            get
            {
                if (IsWindows) return WindowsNative.IsKeyPressed(WindowsNative.VK_MENU);
                if (IsMacOS) return MacNative.IsModifierPressed(MacNative.kCGEventFlagMaskAlternate);
                return false;
            }
        }

        public static bool IsCommandPressed
        {
            get
            {
                if (IsMacOS) return MacNative.IsModifierPressed(MacNative.kCGEventFlagMaskCommand);
                return false; // Windows doesn't have Command key
            }
        }

        private static class WindowsNative
        {
            public const int VK_SHIFT = 0x10;
            public const int VK_CONTROL = 0x11;
            public const int VK_MENU = 0x12;       // Alt
            public const int VK_CAPITAL = 0x14;    // Caps Lock
            public const int VK_NUMLOCK = 0x90;
            public const int VK_SCROLL = 0x91;

            [DllImport("user32.dll")]
            private static extern short GetKeyState(int nVirtKey);

            public static bool IsKeyToggled(int vkCode) => (GetKeyState(vkCode) & 0x0001) != 0;
            public static bool IsKeyPressed(int vkCode) => (GetKeyState(vkCode) & 0x8000) != 0;
        }

        private static class MacNative
        {
            public const ulong kCGEventFlagMaskShift = 0x00020000;
            public const ulong kCGEventFlagMaskControl = 0x00040000;
            public const ulong kCGEventFlagMaskAlternate = 0x00080000;  // Option/Alt
            public const ulong kCGEventFlagMaskCommand = 0x00100000;
            public const ulong kCGEventFlagMaskAlphaShift = 0x00010000; // Caps Lock

            [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
            private static extern ulong CGEventSourceFlagsState(int stateID);

            private const int kCGEventSourceStateCombinedSessionState = 0;

            public static ulong GetCurrentFlags() => CGEventSourceFlagsState(kCGEventSourceStateCombinedSessionState);

            public static bool IsModifierPressed(ulong flag) => (GetCurrentFlags() & flag) != 0;

            public static bool IsCapsLockOn => IsModifierPressed(kCGEventFlagMaskAlphaShift);
        }
    }
}

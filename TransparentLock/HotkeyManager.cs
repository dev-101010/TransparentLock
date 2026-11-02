using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Interop;
using System;

namespace TransparentLock
{
    public static class HotkeyManager
    {
        private const int HotkeyId = 1;
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_NOREPEAT = 0x4000;

        private static bool _registered;
        private static Action? _onHotkey;

        /// <summary>
        /// Registers a hotkey like "Ctrl+Shift+L". An empty string disables the hotkey.
        /// Returns false if the text is invalid or the combination is taken by another application.
        /// </summary>
        public static bool Register(string? hotkey, Action onHotkey)
        {
            Unregister();

            _onHotkey = onHotkey;

            if (string.IsNullOrWhiteSpace(hotkey))
                return true;

            if (!TryParse(hotkey!, out ModifierKeys modifiers, out Key key))
                return false;

            // ModifierKeys uses the same bit values as the MOD_* flags of RegisterHotKey
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            if (!NativeMethods.RegisterHotKey(IntPtr.Zero, HotkeyId, (uint)modifiers | MOD_NOREPEAT, vk))
                return false;

            ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
            _registered = true;
            return true;
        }

        public static void Unregister()
        {
            if (!_registered)
                return;

            NativeMethods.UnregisterHotKey(IntPtr.Zero, HotkeyId);
            ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
            _registered = false;
        }

        private static void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message == WM_HOTKEY && (int)msg.wParam == HotkeyId)
            {
                _onHotkey?.Invoke();
                handled = true;
            }
        }

        /// <summary>Builds the stored representation, e.g. "Ctrl+Shift+L".</summary>
        public static string Format(ModifierKeys modifiers, Key key)
        {
            var parts = new List<string>();
            if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
            if (key != Key.None) parts.Add(KeyToText(key));
            return string.Join("+", parts);
        }

        /// <summary>Human-readable, localized form, e.g. "Ctrl + Shift + L" / "Strg + Umschalt + L".</summary>
        public static string ToDisplay(string? hotkey)
        {
            if (string.IsNullOrWhiteSpace(hotkey))
                return string.Empty;

            return TryParse(hotkey!, out var modifiers, out var key)
                ? ToDisplay(modifiers, key)
                : hotkey!;
        }

        public static string ToDisplay(ModifierKeys modifiers, Key key)
        {
            var parts = new List<string>();
            if ((modifiers & ModifierKeys.Control) != 0) parts.Add(Strings.KeyCtrl);
            if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((modifiers & ModifierKeys.Shift) != 0) parts.Add(Strings.KeyShift);
            if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
            if (key != Key.None) parts.Add(KeyToText(key));
            return string.Join(" + ", parts);
        }

        public static bool TryParse(string hotkey, out ModifierKeys modifiers, out Key key)
        {
            modifiers = ModifierKeys.None;
            key = Key.None;

            string[] parts = hotkey.Split('+');

            // Last part = key, everything before = modifiers
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim().ToUpperInvariant();

                if (i < parts.Length - 1)
                {
                    switch (part)
                    {
                        case "CTRL":
                        case "CONTROL":
                            modifiers |= ModifierKeys.Control;
                            break;
                        case "SHIFT":
                            modifiers |= ModifierKeys.Shift;
                            break;
                        case "ALT":
                            modifiers |= ModifierKeys.Alt;
                            break;
                        case "WIN":
                        case "WINDOWS":
                            modifiers |= ModifierKeys.Windows;
                            break;
                        default:
                            return false;
                    }
                }
                else if (!TryParseKey(part, out key))
                {
                    return false;
                }
            }

            return key != Key.None;
        }

        private static string KeyToText(Key key)
        {
            if (key >= Key.D0 && key <= Key.D9)
                return ((char)('0' + (key - Key.D0))).ToString();

            return key.ToString();
        }

        private static bool TryParseKey(string keyText, out Key key)
        {
            key = Key.None;

            // Digits first: Enum.TryParse would treat "5" as the numeric enum value 5
            if (keyText.Length == 1 && keyText[0] >= '0' && keyText[0] <= '9')
            {
                key = Key.D0 + (keyText[0] - '0');
                return true;
            }

            if (int.TryParse(keyText, out _))
                return false;

            return Enum.TryParse(keyText, true, out key) && key != Key.None;
        }
    }
}

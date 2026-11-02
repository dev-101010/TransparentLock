using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System;

namespace TransparentLock
{
    public partial class OptionsWindow : Window
    {
        // Stored format, e.g. "Ctrl+Shift+L"; empty = no hotkey
        private string _hotkey;

        // State when the window was opened, so a change made in the tray meanwhile is not overwritten
        private readonly bool _initialKeepAwake;

        private static App App => (App)Application.Current;

        public OptionsWindow()
        {
            InitializeComponent();

            var s = SettingsManager.Current;

            AutoLockIdleBox.Text = s.AutoLockIdleSeconds.ToString(CultureInfo.InvariantCulture);
            PasswordPhaseBox.Text = s.PasswordPhaseSeconds.ToString(CultureInfo.InvariantCulture);
            MaxAttemptsBox.Text = s.MaxAttemptsBeforeBackoff.ToString(CultureInfo.InvariantCulture);
            InitialBackoffBox.Text = s.InitialBackoffSeconds.ToString(CultureInfo.InvariantCulture);
            WindowsLockAfterBox.Text = s.WindowsLockAfterMinutes.ToString(CultureInfo.InvariantCulture);
            AutostartToggle.IsChecked = Autostart.IsEnabled;
            _initialKeepAwake = App.KeepAwake;
            KeepAwakeToggle.IsChecked = _initialKeepAwake;

            _hotkey = s.Hotkey ?? string.Empty;
            ShowHotkey();

            foreach (var box in new[] { AutoLockIdleBox, PasswordPhaseBox, MaxAttemptsBox, InitialBackoffBox, WindowsLockAfterBox })
            {
                box.PreviewTextInput += OnNumberPreviewTextInput;
                DataObject.AddPastingHandler(box, OnNumberPasting);
            }

            // Keep the window on screen on small displays; the content scrolls instead
            MaxHeight = SystemParameters.WorkArea.Height - 40;

            SourceInitialized += (_, _) => ApplyTitleBarTheme();
            ThemeManager.ThemeChanged += ApplyTitleBarTheme;
            Closed += (_, _) =>
            {
                ThemeManager.ThemeChanged -= ApplyTitleBarTheme;
                // The hotkey is suspended while it is being recorded
                App.ReloadHotkey();
            };

            UpdatePasswordStatus();
        }

        public void BringToFront()
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            Activate();
            NativeMethods.ForceForeground(new WindowInteropHelper(this).Handle);
        }

        private void ApplyTitleBarTheme()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            NativeMethods.ApplyTitleBarTheme(hwnd, dark: !ThemeManager.IsLight, ThemeManager.CaptionColor);
        }

        private void UpdatePasswordStatus()
        {
            bool set = SettingsManager.IsPasswordConfigured();

            PasswordStatusText.Text = set ? Strings.PasswordStatusSet : Strings.PasswordStatusNotSet;
            PasswordStatusPillText.Text = set ? Strings.PillSet : Strings.PillNotSet;
            PasswordStatusPill.SetResourceReference(Border.BackgroundProperty, set ? "Brush.SuccessBg" : "Brush.DangerBg");
            PasswordStatusPillText.SetResourceReference(TextBlock.ForegroundProperty, set ? "Brush.Success" : "Brush.Danger");
            RemovePasswordButton.Visibility = set ? Visibility.Visible : Visibility.Collapsed;
        }

        // ----- Save / cancel -----

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            ShowError(null);

            if (!TryReadNumber(AutoLockIdleBox, 0, Strings.AutoLockTitle, out int idleSecs) ||
                !TryReadNumber(PasswordPhaseBox, 5, Strings.PromptDurationTitle, out int passSecs) ||
                !TryReadNumber(MaxAttemptsBox, 1, Strings.AttemptsTitle, out int maxAttempts) ||
                !TryReadNumber(InitialBackoffBox, 3, Strings.BackoffTitle, out int initBackoff) ||
                !TryReadNumber(WindowsLockAfterBox, 0, Strings.WindowsLockTitle, out int windowsLockAfter))
                return;

            var newPassword = NewPasswordBox.Password;
            if (newPassword.Length > 0 || ConfirmPasswordBox.Password.Length > 0)
            {
                if (!PasswordPolicy.IsSafePassword(newPassword, out var error))
                {
                    ShowError(error, NewPasswordBox);
                    return;
                }

                if (newPassword != ConfirmPasswordBox.Password)
                {
                    ShowError(Strings.ErrorPasswordsDontMatch, ConfirmPasswordBox);
                    return;
                }
            }

            // Register first, so a hotkey that is taken by another application is reported before saving
            if (!App.TryRegisterHotkey(_hotkey))
            {
                ShowError(Strings.ErrorHotkeyTaken(HotkeyManager.ToDisplay(_hotkey)), HotkeyBox);
                return;
            }

            bool autostart = AutostartToggle.IsChecked == true;
            if (autostart != Autostart.IsEnabled && !Autostart.SetEnabled(autostart))
            {
                ShowError(Strings.ErrorAutostart, AutostartToggle);
                return;
            }

            var s = SettingsManager.Current;

            if (newPassword.Length > 0)
                PasswordHelper.SetPassword(s, newPassword);

            s.AutoLockIdleSeconds = idleSecs;
            s.PasswordPhaseSeconds = passSecs;
            s.MaxAttemptsBeforeBackoff = maxAttempts;
            s.InitialBackoffSeconds = initBackoff;
            s.WindowsLockAfterMinutes = windowsLockAfter;
            s.Hotkey = _hotkey;

            if (!SettingsManager.Save())
            {
                ShowError(Strings.ErrorSave);
                return;
            }

            bool keepAwake = KeepAwakeToggle.IsChecked == true;
            if (keepAwake != _initialKeepAwake)
                App.SetKeepAwake(keepAwake);

            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnRemovePasswordClick(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(this,
                    Strings.RemovePasswordQuestion,
                    Strings.AppName,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            var s = SettingsManager.Current;
            s.PasswordHash = string.Empty;
            s.PasswordSalt = string.Empty;
            if (!SettingsManager.Save())
                ShowError(Strings.ErrorSave);
            UpdatePasswordStatus();
        }

        private bool TryReadNumber(TextBox box, int min, string label, out int value)
        {
            if (int.TryParse(box.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min)
                return true;

            ShowError(Strings.ErrorNumber(label, min), box);
            return false;
        }

        private void ShowError(string? message, Control? focus = null)
        {
            ErrorText.Text = message ?? string.Empty;

            if (focus == null)
                return;

            focus.Focus();
            if (focus is TextBox textBox && !textBox.IsReadOnly)
                textBox.SelectAll();
        }

        // ----- Number boxes -----

        private static bool IsDigits(string text)
        {
            foreach (char c in text)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return true;
        }

        private void OnNumberPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsDigits(e.Text);
        }

        private void OnNumberPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!(e.DataObject.GetData(typeof(string)) is string text) || !IsDigits(text.Trim()))
                e.CancelCommand();
        }

        // ----- Hotkey recording -----

        private void ShowHotkey()
        {
            HotkeyBox.Text = _hotkey.Length > 0 ? HotkeyManager.ToDisplay(_hotkey) : Strings.HotkeyNone;
        }

        private void HotkeyBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Otherwise pressing the current hotkey would lock the screen instead of being recorded
            App.SuspendHotkey();
            HotkeyBox.Text = Strings.HotkeyPressKeys;
        }

        private void HotkeyBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ShowHotkey();
            App.ReloadHotkey();
        }

        private void HotkeyBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var modifiers = Keyboard.Modifiers;

            // Keep keyboard navigation working
            if (key == Key.Tab && (modifiers == ModifierKeys.None || modifiers == ModifierKeys.Shift))
                return;

            e.Handled = true;

            if ((key == Key.Back || key == Key.Delete) && modifiers == ModifierKeys.None)
            {
                _hotkey = string.Empty;
                ShowHotkey();
                return;
            }

            if (IsModifierKey(key))
            {
                HotkeyBox.Text = HotkeyManager.ToDisplay(modifiers, Key.None) + " + …";
                return;
            }

            if (modifiers == ModifierKeys.None)
            {
                HotkeyBox.Text = Strings.HotkeyNeedsModifier;
                return;
            }

            _hotkey = HotkeyManager.Format(modifiers, key);
            ShowHotkey();
        }

        private void HotkeyBox_OnPreviewKeyUp(object sender, KeyEventArgs e)
        {
            // All keys released without completing a combination -> show the current value again
            if (Keyboard.Modifiers == ModifierKeys.None)
                ShowHotkey();
        }

        private static bool IsModifierKey(Key key) =>
            key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
    }
}

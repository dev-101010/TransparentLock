using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System;

namespace TransparentLock
{
    public partial class MainWindow : Window
    {
        private enum Phase
        {
            Idle,     // only the "locked" text is shown, input is swallowed
            Prompt,   // password box visible for PasswordPhaseSeconds
            Lockout   // too many wrong passwords, input blocked for a while
        }

        private readonly System.Drawing.Rectangle _screenBounds;
        private readonly DispatcherTimer _phaseTimer;
        private Phase _phase = Phase.Idle;
        private DateTime _promptEndsUtc;
        private bool _verifying;
        private bool _closed;
        private IntPtr _hwnd;

        private static App App => (App)Application.Current;
        private static Settings Settings => SettingsManager.Current;

        /// <summary>Set by the app right before closing; any other close attempt is refused.</summary>
        internal bool AllowClose { get; set; }

        public bool IsPrimaryScreen { get; }

        public bool IsInteracting => _phase != Phase.Idle;

        public MainWindow(System.Drawing.Rectangle screenBounds, bool isPrimaryScreen)
        {
            InitializeComponent();

            _screenBounds = screenBounds;
            IsPrimaryScreen = isPrimaryScreen;

            // Rough initial placement; the exact pixel bounds are applied once the handle exists
            // (WPF works in DIPs, which differ per monitor with mixed DPI settings).
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = screenBounds.Left;
            Top = screenBounds.Top;
            Width = screenBounds.Width;
            Height = screenBounds.Height;

            _phaseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _phaseTimer.Tick += OnPhaseTick;

            PreviewKeyDown += OnPreviewKeyDown;
            PreviewMouseDown += OnPreviewMouseDown;
            PreviewMouseWheel += OnPreviewMouseWheel;

            SourceInitialized += (_, _) =>
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                KeepOnTop();
            };
            Loaded += (_, _) => KeepOnTop();
            DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(KeepOnTop), DispatcherPriority.Background);
            Deactivated += (_, _) => App.RequestRefocus();

            Closing += (_, e) =>
            {
                if (!AllowClose)
                    e.Cancel = true;
            };
            Closed += (_, _) =>
            {
                _closed = true;
                _phaseTimer.Stop();
            };
        }

        public void KeepOnTop()
        {
            NativeMethods.KeepTopmost(_hwnd, _screenBounds);
        }

        public void TakeFocus()
        {
            if (_hwnd == IntPtr.Zero)
                return;

            NativeMethods.ForceForeground(_hwnd);
            Activate();

            if (_phase == Phase.Prompt && PasswordBox.IsEnabled)
                PasswordBox.Focus();
            else
                Focus();
        }

        // ----- Input -----

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Always treat dangerous combos as "breakers" -> real lock
            if (IsDangerousKey(key))
            {
                App.TriggerRealLock();
                e.Handled = true;
                return;
            }

            // Let the password box handle typing and Enter itself
            if (_phase == Phase.Prompt && PasswordBox.IsKeyboardFocusWithin)
                return;

            OnUserActivity();
            e.Handled = true;
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Mouse is never "dangerous" by itself -> just start password phase.
            OnUserActivity();
            e.Handled = true;
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            OnUserActivity();
            e.Handled = true;
        }

        private void OnUserActivity()
        {
            if (_verifying)
                return;

            if (App.LockSession.IsLockedOut)
            {
                if (_phase != Phase.Lockout)
                    EnterLockout();
                return;
            }

            if (_phase == Phase.Idle)
                EnterPrompt(null);
            else if (_phase == Phase.Prompt)
                PasswordBox.Focus();
        }

        // ----- Phases -----

        private void EnterPrompt(string? message)
        {
            _phase = Phase.Prompt;

            HintText.Visibility = Visibility.Collapsed;
            PasswordPanel.Visibility = Visibility.Visible;
            CountdownBar.SetResourceReference(BackgroundProperty, "Brush.Accent");

            PasswordBox.IsEnabled = true;
            PasswordBox.Clear();
            SetStatus(message, isError: true);

            RestartCountdown();
            _phaseTimer.Start();

            PasswordBox.Focus();
        }

        private void EnterLockout()
        {
            _phase = Phase.Lockout;

            HintText.Visibility = Visibility.Collapsed;
            PasswordPanel.Visibility = Visibility.Visible;
            CountdownBar.SetResourceReference(BackgroundProperty, "Brush.Danger");

            PasswordBox.Clear();
            PasswordBox.IsEnabled = false;
            Focus();

            AnimateCountdown(App.LockSession.LockoutRemaining);
            UpdateLockoutText();
            _phaseTimer.Start();
        }

        private void GoIdle()
        {
            _phase = Phase.Idle;
            _phaseTimer.Stop();

            PasswordPanel.Visibility = Visibility.Collapsed;
            HintText.Visibility = Visibility.Visible;

            PasswordBox.Clear();
            SetStatus(null, isError: false);
            CountdownScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);

            Focus();
        }

        private void OnPhaseTick(object? sender, EventArgs e)
        {
            switch (_phase)
            {
                case Phase.Prompt:
                    var remaining = _promptEndsUtc - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        if (!_verifying)
                            GoIdle();
                    }
                    else
                    {
                        CountdownText.Text = $"{Math.Ceiling(remaining.TotalSeconds)} s";
                    }
                    break;

                case Phase.Lockout:
                    if (App.LockSession.IsLockedOut)
                        UpdateLockoutText();
                    else
                        EnterPrompt(null);
                    break;
            }
        }

        private void RestartCountdown()
        {
            var duration = TimeSpan.FromSeconds(Math.Max(5, Settings.PasswordPhaseSeconds));
            _promptEndsUtc = DateTime.UtcNow + duration;
            CountdownText.Text = $"{duration.TotalSeconds} s";
            AnimateCountdown(duration);
        }

        private void AnimateCountdown(TimeSpan duration)
        {
            CountdownScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1, 0, new Duration(duration)));
        }

        private void UpdateLockoutText()
        {
            var seconds = Math.Ceiling(App.LockSession.LockoutRemaining.TotalSeconds);
            SetStatus(Strings.OverlayLockout(seconds), isError: true);
            CountdownText.Text = $"{seconds} s";
        }

        private void SetStatus(string? text, bool isError)
        {
            StatusText.Text = text ?? string.Empty;
            StatusText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, isError ? "Brush.Danger" : "Brush.TextSecondary");
        }

        private void Shake()
        {
            var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(360) };
            foreach (var (offset, ms) in new[] { (-10.0, 60), (10.0, 120), (-6.0, 180), (6.0, 240), (-3.0, 300), (0.0, 360) })
                animation.KeyFrames.Add(new LinearDoubleKeyFrame(offset, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms))));

            ShakeTransform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        // ----- Password -----

        private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            // Typing keeps the prompt open
            if (_phase == Phase.Prompt && !_verifying && PasswordBox.Password.Length > 0)
                RestartCountdown();
        }

        private async void PasswordBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            e.Handled = true;

            if (_verifying || _phase != Phase.Prompt)
                return;

            var entered = PasswordBox.Password;
            if (entered.Length == 0)
                return;

            _verifying = true;
            PasswordBox.IsEnabled = false;
            SetStatus(Strings.OverlayChecking, isError: false);

            bool ok;
            try
            {
                // PBKDF2 takes a moment; keep the UI thread (and the keyboard hook) responsive
                var settings = Settings;
                ok = await Task.Run(() => PasswordHelper.VerifyPassword(settings, entered));
            }
            finally
            {
                _verifying = false;
            }

            if (_closed)
                return;

            if (ok)
            {
                App.Unlock();
                return;
            }

            Shake();

            if (App.LockSession.RegisterFailure(Settings))
            {
                EnterLockout();
                return;
            }

            int left = App.LockSession.AttemptsLeft(Settings);
            PasswordBox.IsEnabled = true;
            PasswordBox.Clear();
            PasswordBox.Focus();
            RestartCountdown();
            SetStatus(Strings.OverlayWrongPassword(left), isError: true);
        }

        private bool IsDangerousKey(Key key)
        {
            var modifiers = Keyboard.Modifiers;

            // Bare Ctrl is dangerous (prevents Ctrl+Alt+Del from fully forming as SAS)
            if (key == Key.LeftCtrl || key == Key.RightCtrl)
                return true;

            // Windows keys: any Win-based shortcut is considered dangerous
            if (key == Key.LWin || key == Key.RWin)
                return true;

            // Alt+Tab, Alt+F4, Alt+Esc
            if ((modifiers & ModifierKeys.Alt) != 0)
            {
                if (key == Key.Tab || key == Key.F4 || key == Key.Escape)
                    return true;
            }

            // Ctrl+Shift+Esc (Task Manager)
            if (key == Key.Escape &&
                (modifiers & ModifierKeys.Control) != 0 &&
                (modifiers & ModifierKeys.Shift) != 0)
                return true;

            // Ctrl+Esc (classic Start menu)
            if (key == Key.Escape && (modifiers & ModifierKeys.Control) != 0)
                return true;

            // Function keys as potentially dangerous
            if (key is >= Key.F1 and <= Key.F12)
                return true;

            return false;
        }
    }
}

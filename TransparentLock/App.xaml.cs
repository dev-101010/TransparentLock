using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Resources;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using System;
using System.Drawing;
using System.IO;
using Microsoft.Win32;

namespace TransparentLock
{
    public partial class App : System.Windows.Application
    {
        private SingleInstance? _singleInstance;

        private WinForms.NotifyIcon? _trayIcon;
        private TrayMenu? _trayMenu;
        private DispatcherTimer? _idleTimer;
        private DispatcherTimer? _guardTimer;
        private bool _keepAwake;

        private bool _sessionIsLocked;
        private DateTime _ignoreIdleUntilUtc = DateTime.MinValue;

        // Windows silently removes low-level hooks that time out, so they are reinstalled regularly
        private static readonly TimeSpan HookRefreshInterval = TimeSpan.FromSeconds(5);
        // If the overlay cannot get the foreground back within this time (e.g. virtual desktop
        // switched via touchpad gesture), the real Windows lock takes over
        private static readonly TimeSpan ForegroundLossTimeout = TimeSpan.FromSeconds(2);

        // Only installed while the overlay is shown
        private KeyboardHook? _keyboardHook;
        private MouseHook? _mouseHook;
        private ForegroundWatcher? _foregroundWatcher;
        private bool _hookWarningShown;
        private DateTime _lastHookRefreshUtc = DateTime.MinValue;

        private readonly List<MainWindow> _overlays = new List<MainWindow>();
        private volatile bool _overlayVisible; // readable from any thread (crash handler)
        private bool _activatingOverlay;
        private bool _realLockPending;
        private DateTime _lastRefocusUtc = DateTime.MinValue;
        private DateTime _overlayStartedUtc;
        private DateTime _foregroundLostSinceUtc = DateTime.MinValue;

        private OptionsWindow? _optionsWindow;

        public LockSession LockSession { get; } = new LockSession();

        public bool OverlayActive => _overlays.Count > 0;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _singleInstance = SingleInstance.TryAcquire(
                () => Dispatcher.BeginInvoke(new Action(OpenOptionsWindow)));

            if (_singleInstance == null)
            {
                // Already running for this user in this session -> show the running instance instead
                SingleInstance.SignalRunningInstance();
                Shutdown();
                return;
            }

            // Fail secure: a crash must never leave the screen unlocked
            DispatcherUnhandledException += (_, args) => LockOnCrash(args.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, args) => LockOnCrash(args.ExceptionObject as Exception);

            ThemeManager.Initialize(this);
            SettingsManager.Load();

            CreateTrayIcon();

            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            SetupTimers();

            // Restore the last state of the "Keep screen awake" switch
            if (SettingsManager.Current.KeepAwake)
                SetKeepAwake(true);

            if (!ReloadHotkey())
            {
                ShowBalloon(Strings.BalloonHotkeyTaken(HotkeyManager.ToDisplay(SettingsManager.Current.Hotkey)),
                    WinForms.ToolTipIcon.Warning);
            }

            if (!SettingsManager.IsPasswordConfigured())
                OpenOptionsWindow();
        }

        public bool ReloadHotkey()
        {
            bool ok = TryRegisterHotkey(SettingsManager.Current.Hotkey);
            UpdateTrayState();
            return ok;
        }

        public bool TryRegisterHotkey(string hotkey)
        {
            return HotkeyManager.Register(hotkey, ActivateOverlay);
        }

        public void SuspendHotkey()
        {
            HotkeyManager.Unregister();
        }

        // ----- Tray -----

        private void CreateTrayIcon()
        {
            _trayMenu = new TrayMenu(this);

            _trayIcon = new WinForms.NotifyIcon
            {
                Text = "TransparentLock",
                Icon = LoadTrayIcon(),
                Visible = true
            };

            _trayIcon.MouseUp += (_, e) =>
            {
                if (e.Button == WinForms.MouseButtons.Right)
                    ShowTrayMenu();
            };
            _trayIcon.MouseDoubleClick += (_, e) =>
            {
                if (e.Button == WinForms.MouseButtons.Left)
                    ActivateOverlay();
            };

            UpdateTrayState();
        }

        private static Icon LoadTrayIcon()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Resources/TransparentLock.ico");
                StreamResourceInfo? info = GetResourceStream(uri);

                if (info != null)
                {
                    using var src = info.Stream;
                    using var ms = new MemoryStream();
                    src.CopyTo(ms);
                    ms.Position = 0;
                    return new Icon(ms, WinForms.SystemInformation.SmallIconSize);
                }
            }
            catch
            {
                // fall through
            }

            return SystemIcons.Shield;
        }

        private void ShowTrayMenu()
        {
            // While locked the tray must not offer Exit / Settings
            if (OverlayActive)
                return;

            UpdateTrayState();
            _trayMenu?.Show();
        }

        private void UpdateTrayState()
        {
            var settings = SettingsManager.Current;
            bool passwordSet = SettingsManager.IsPasswordConfigured();
            string hotkey = HotkeyManager.ToDisplay(settings.Hotkey);

            _trayMenu?.Update(passwordSet, hotkey, _keepAwake, settings.AutoLockIdleSeconds);

            if (_trayIcon != null)
            {
                string text = !passwordSet ? Strings.TrayTooltipNoPassword
                    : hotkey.Length > 0 ? $"{Strings.AppName} – {hotkey}"
                    : Strings.AppName;

                // NotifyIcon.Text is limited to 63 characters on .NET Framework
                _trayIcon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
            }
        }

        private void ShowBalloon(string text, WinForms.ToolTipIcon icon)
        {
            _trayIcon?.ShowBalloonTip(5000, Strings.AppName, text, icon);
        }

        public bool KeepAwake => _keepAwake;

        public void SetKeepAwake(bool keepAwake)
        {
            _keepAwake = keepAwake;

            if (SettingsManager.Current.KeepAwake != keepAwake)
            {
                SettingsManager.Current.KeepAwake = keepAwake;
                SettingsManager.Save();
            }

            if (keepAwake)
                NativeMethods.PreventSleep();
            else
                NativeMethods.AllowSleep();

            UpdateTrayState();
        }

        public void OpenOptionsWindow()
        {
            if (OverlayActive)
                return;

            if (_optionsWindow == null)
            {
                _optionsWindow = new OptionsWindow();
                _optionsWindow.Closed += (_, _) =>
                {
                    _optionsWindow = null;
                    UpdateTrayState();
                };
                _optionsWindow.Show();
            }

            _optionsWindow.BringToFront();
        }

        // ----- Timers -----

        private void SetupTimers()
        {
            _idleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _idleTimer.Tick += (_, _) => CheckIdle();
            _idleTimer.Start();

            // While locked: periodically re-assert z-order and focus (backup for the foreground hook)
            _guardTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _guardTimer.Tick += (_, _) => OnGuardTick();
        }

        private void OnGuardTick()
        {
            if (!OverlayActive)
                return;

            var now = DateTime.UtcNow;

            // Optional: hand over to the real Windows lock after a while
            int lockAfterMinutes = SettingsManager.Current.WindowsLockAfterMinutes;
            if (lockAfterMinutes > 0 && now - _overlayStartedUtc >= TimeSpan.FromMinutes(lockAfterMinutes))
            {
                TriggerRealLock();
                return;
            }

            if (now - _lastHookRefreshUtc >= HookRefreshInterval)
                InstallInputHooks();

            BringOverlayToFront(force: false);

            // Fail secure: focus could not be taken back -> real lock
            if (NativeMethods.IsForegroundInCurrentProcess())
                _foregroundLostSinceUtc = DateTime.MinValue;
            else if (_foregroundLostSinceUtc == DateTime.MinValue)
                _foregroundLostSinceUtc = now;
            else if (now - _foregroundLostSinceUtc >= ForegroundLossTimeout)
                TriggerRealLock();
        }

        private void LockOnCrash(Exception? ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);

            if (_overlayVisible)
                NativeMethods.LockWorkStation();
        }

        private void CheckIdle()
        {
            if (OverlayActive || _sessionIsLocked)
                return;

            if (DateTime.UtcNow < _ignoreIdleUntilUtc)
                return;

            var settings = SettingsManager.Current;

            if (settings.AutoLockIdleSeconds <= 0)
                return;

            if (!SettingsManager.IsPasswordConfigured())
                return;

            if (NativeMethods.GetIdleTimeInSeconds() < settings.AutoLockIdleSeconds)
                return;

            // Windows lock screen or disconnected session (user switch / RDP) -> nothing to do
            if (NativeMethods.IsWorkstationLocked())
                return;

            ActivateOverlay();
        }

        // ----- Overlay -----

        public void ActivateOverlay()
        {
            if (OverlayActive || _activatingOverlay || _sessionIsLocked)
                return;

            if (!SettingsManager.IsPasswordConfigured())
            {
                ShowBalloon(Strings.BalloonSetPasswordFirst, WinForms.ToolTipIcon.Warning);
                OpenOptionsWindow();
                return;
            }

            _activatingOverlay = true;
            try
            {
                _trayMenu?.Close();
                _optionsWindow?.Close();

                _overlayStartedUtc = DateTime.UtcNow;
                _foregroundLostSinceUtc = DateTime.MinValue;
                CreateOverlays();
                StartGuards();
                BringOverlayToFront(force: true);
            }
            finally
            {
                _activatingOverlay = false;
            }

            UpdateTrayState();
        }

        private void CreateOverlays()
        {
            foreach (var screen in WinForms.Screen.AllScreens)
            {
                var overlay = new MainWindow(screen.Bounds, screen.Primary);
                overlay.Closed += OnOverlayClosed;
                _overlays.Add(overlay);
                _overlayVisible = true;
                overlay.Show();
            }
        }

        private void OnOverlayClosed(object? sender, EventArgs e)
        {
            if (sender is MainWindow overlay)
                _overlays.Remove(overlay);

            if (!OverlayActive)
            {
                StopGuards();
                UpdateTrayState();
            }
        }

        /// <summary>Called by an overlay after the correct password was entered.</summary>
        public void Unlock()
        {
            LockSession.Reset();
            CloseAllOverlays();
        }

        public void CloseAllOverlays()
        {
            var overlays = _overlays.ToArray();
            _overlays.Clear();

            foreach (var overlay in overlays)
            {
                overlay.Closed -= OnOverlayClosed;
                overlay.AllowClose = true;
                overlay.Close();
            }

            StopGuards();
            UpdateTrayState();
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!OverlayActive)
                    return;

                // Monitor added/removed or resolution changed: cover the new layout
                // before removing the old windows so there is no unlocked gap.
                var old = _overlays.ToArray();
                _overlays.Clear();
                CreateOverlays();

                foreach (var overlay in old)
                {
                    overlay.Closed -= OnOverlayClosed;
                    overlay.AllowClose = true;
                    overlay.Close();
                }

                BringOverlayToFront(force: true);
            }));
        }

        // ----- Guards (z-order, focus, keyboard) -----

        private void StartGuards()
        {
            InstallInputHooks();

            if (_foregroundWatcher == null)
            {
                try
                {
                    _foregroundWatcher = new ForegroundWatcher(_ => RequestRefocus());
                }
                catch (Exception ex)
                {
                    // The guard timer still re-asserts the focus periodically
                    System.Diagnostics.Debug.WriteLine(ex);
                }
            }

            _guardTimer?.Start();
        }

        /// <summary>
        /// Installs (or reinstalls) the keyboard and mouse hooks. The new hook is installed
        /// before the old one is removed, so there is no unprotected gap.
        /// </summary>
        private void InstallInputHooks()
        {
            _lastHookRefreshUtc = DateTime.UtcNow;

            try
            {
                var keyboardHook = new KeyboardHook(OnGlobalKey);
                _keyboardHook?.Dispose();
                _keyboardHook = keyboardHook;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);

                if (_keyboardHook == null && !_hookWarningShown)
                {
                    _hookWarningShown = true;
                    ShowBalloon(Strings.BalloonHookFailed, WinForms.ToolTipIcon.Warning);
                }
            }

            try
            {
                var mouseHook = new MouseHook(OnGlobalMouse);
                _mouseHook?.Dispose();
                _mouseHook = mouseHook;
            }
            catch (Exception ex)
            {
                // The overlay still covers the screens; this only affects shell UI above it
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void StopGuards()
        {
            _overlayVisible = OverlayActive;
            _guardTimer?.Stop();

            _keyboardHook?.Dispose();
            _keyboardHook = null;

            _mouseHook?.Dispose();
            _mouseHook = null;

            _foregroundWatcher?.Dispose();
            _foregroundWatcher = null;
        }

        /// <summary>Another application took the foreground (or an overlay was deactivated).</summary>
        public void RequestRefocus()
        {
            if (!OverlayActive)
                return;

            var now = DateTime.UtcNow;
            if (now - _lastRefocusUtc < TimeSpan.FromMilliseconds(100))
                return;
            _lastRefocusUtc = now;

            Dispatcher.BeginInvoke(new Action(() => BringOverlayToFront(force: false)), DispatcherPriority.Input);
        }

        private void BringOverlayToFront(bool force)
        {
            if (!OverlayActive)
                return;

            foreach (var overlay in _overlays)
                overlay.KeepOnTop();

            // Switching between our own overlays (other monitor) is fine
            if (!force && NativeMethods.IsForegroundInCurrentProcess())
                return;

            var target = _overlays.FirstOrDefault(o => o.IsInteracting)
                         ?? _overlays.FirstOrDefault(o => o.IsPrimaryScreen)
                         ?? _overlays[0];
            target.TakeFocus();
        }

        private bool OnGlobalKey(int vkCode, bool isDown, bool isInjected)
        {
            if (!OverlayActive)
                return false;

            if (isDown && (isInjected || IsDangerousVk(vkCode)))
            {
                TriggerRealLock();
                return false;
            }

            if (!NativeMethods.IsForegroundInCurrentProcess())
            {
                // Some other window (e.g. a chat app that came to the front) would receive
                // this key -> swallow it and take the focus back.
                if (isDown)
                    RequestRefocus();
                return true;
            }

            return false;
        }

        private bool OnGlobalMouse(int x, int y, bool isButtonDown, bool isInjected)
        {
            if (!OverlayActive)
                return false;

            // Synthetic clicks (remote tools, scripts) are treated like injected keys
            if (isInjected)
            {
                if (isButtonDown)
                    TriggerRealLock();
                return true;
            }

            // Click on something above the overlay (notification, shell UI) -> swallow it
            if (!NativeMethods.IsWindowAtPointInCurrentProcess(x, y))
            {
                if (isButtonDown)
                    RequestRefocus();
                return true;
            }

            return false;
        }

        public void TriggerRealLock()
        {
            if (_realLockPending)
                return;

            _realLockPending = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    NativeMethods.LockWorkStation();
                }
                finally
                {
                    CloseAllOverlays();
                    _realLockPending = false;
                }
            }), DispatcherPriority.Normal);
        }

        private static bool IsDangerousVk(int vk)
        {
            // Ctrl
            if (vk == 0xA2 || vk == 0xA3) // VK_LCONTROL, VK_RCONTROL
                return true;

            // Windows keys
            if (vk == 0x5B || vk == 0x5C) // VK_LWIN, VK_RWIN
                return true;

            // Alt
            if (vk == 0xA4 || vk == 0xA5) // VK_LMENU, VK_RMENU
                return true;

            // Esc (optional)
            if (vk == 0x1B)
                return true;

            // F1–F12
            if (vk >= 0x70 && vk <= 0x7B)
                return true;

            return false;
        }

        // ----- Session -----

        private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
        {
            var reason = e.Reason;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                switch (reason)
                {
                    case SessionSwitchReason.SessionLock:
                    case SessionSwitchReason.ConsoleDisconnect:
                    case SessionSwitchReason.RemoteDisconnect:
                        _sessionIsLocked = true;
                        CloseAllOverlays();
                        break;

                    case SessionSwitchReason.SessionUnlock:
                    case SessionSwitchReason.ConsoleConnect:
                    case SessionSwitchReason.RemoteConnect:
                        // CheckIdle additionally verifies that the lock screen is gone
                        _sessionIsLocked = false;
                        _ignoreIdleUntilUtc = DateTime.UtcNow.AddSeconds(5);
                        break;
                }
            }));
        }

        // ----- Shutdown -----

        public void ExitApplication()
        {
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_singleInstance != null)
            {
                _idleTimer?.Stop();
                StopGuards();
                HotkeyManager.Unregister();

                if (_keepAwake)
                    NativeMethods.AllowSleep();

                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                    _trayIcon = null;
                }

                SystemEvents.SessionSwitch -= OnSessionSwitch;
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

                _singleInstance.Dispose();
                _singleInstance = null;
            }

            base.OnExit(e);
        }
    }
}

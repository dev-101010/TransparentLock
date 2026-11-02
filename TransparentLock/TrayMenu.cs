using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TransparentLock
{
    /// <summary>
    /// WPF context menu for the tray icon (styled in Themes/Controls.xaml).
    /// </summary>
    internal sealed class TrayMenu
    {
        private readonly ContextMenu _menu;
        private readonly MenuItem _lockItem;
        private readonly MenuItem _keepAwakeItem;
        private readonly TextBlock _statusText;
        private readonly Ellipse _statusDot;
        private bool _updating;

        public TrayMenu(App app)
        {
            _statusDot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _statusText = new TextBlock { FontSize = 12 };
            _statusText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextSecondary");

            var title = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new Image { Source = LoadIcon(32), Width = 20, Height = 20, Margin = new Thickness(0, 0, 10, 0) },
                    new TextBlock { Text = Strings.AppName, FontSize = 14, FontWeight = FontWeights.SemiBold }
                }
            };
            var status = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(30, 4, 0, 0),
                Children = { _statusDot, _statusText }
            };

            var header = new MenuItem { Header = new StackPanel { Children = { title, status } } };
            header.SetResourceReference(FrameworkElement.StyleProperty, "TrayHeaderItem");

            _lockItem = CreateItem(Strings.TrayLockNow, "\uE72E", app.ActivateOverlay);

            _keepAwakeItem = CreateItem(Strings.TrayKeepAwake, "\uE706", null);
            _keepAwakeItem.IsCheckable = true;
            _keepAwakeItem.StaysOpenOnClick = true;
            _keepAwakeItem.Checked += (_, _) => { if (!_updating) app.SetKeepAwake(true); };
            _keepAwakeItem.Unchecked += (_, _) => { if (!_updating) app.SetKeepAwake(false); };

            _menu = new ContextMenu
            {
                Items =
                {
                    header,
                    new Separator(),
                    _lockItem,
                    _keepAwakeItem,
                    CreateItem(Strings.TraySettings, "\uE713", app.OpenOptionsWindow),
                    new Separator(),
                    CreateItem(Strings.TrayExit, "\uE7E8", app.ExitApplication)
                }
            };

            // Without the foreground the menu would not close when clicking somewhere else
            _menu.Opened += (_, _) =>
            {
                if (PresentationSource.FromVisual(_menu) is HwndSource source)
                    NativeMethods.SetForegroundWindow(source.Handle);
            };
        }

        public void Show()
        {
            _menu.Placement = PlacementMode.MousePoint;
            _menu.IsOpen = true;
        }

        public void Close() => _menu.IsOpen = false;

        public void Update(bool passwordSet, string hotkeyDisplay, bool keepAwake, int autoLockSeconds)
        {
            _updating = true;
            _keepAwakeItem.IsChecked = keepAwake;
            _updating = false;

            _lockItem.InputGestureText = hotkeyDisplay;

            if (!passwordSet)
            {
                _statusText.Text = Strings.TrayStatusNoPassword;
                _statusDot.SetResourceReference(Shape.FillProperty, "Brush.Danger");
            }
            else
            {
                _statusText.Text = autoLockSeconds > 0
                    ? Strings.TrayStatusAutoLock(FormatDuration(autoLockSeconds))
                    : Strings.TrayStatusReady;
                _statusDot.SetResourceReference(Shape.FillProperty, "Brush.Success");
            }
        }

        private static MenuItem CreateItem(string header, string glyph, Action? onClick)
        {
            var icon = new TextBlock { Text = glyph };
            icon.SetResourceReference(FrameworkElement.StyleProperty, "Glyph");

            var item = new MenuItem { Header = header, Icon = icon };
            if (onClick != null)
                item.Click += (_, _) => onClick();
            return item;
        }

        private static string FormatDuration(int seconds)
        {
            if (seconds < 60)
                return $"{seconds} s";

            int minutes = seconds / 60;
            int rest = seconds % 60;
            return rest == 0 ? $"{minutes} min" : $"{minutes} min {rest} s";
        }

        internal static ImageSource? LoadIcon(int preferredSize)
        {
            try
            {
                var decoder = BitmapDecoder.Create(new Uri("pack://application:,,,/Resources/TransparentLock.ico"),
                    BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                return decoder.Frames.OrderBy(f => Math.Abs(f.PixelWidth - preferredSize)).First();
            }
            catch
            {
                return null;
            }
        }
    }
}

using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WSLCC.App.Pages;
using WSLCC.Core.Services;
using Forms = System.Windows.Forms;

namespace WSLCC_App;

public sealed partial class MainWindow : Window
{
    private Forms.NotifyIcon? _notifyIcon;
    private bool _forceExit;

    public MainWindow()
    {
        InitializeComponent();
        Title = L.Get("MainWindow.Title");

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 960;
            presenter.PreferredMinimumHeight = 640;
        }
        _ = InitializeAppearanceAsync();
        NavFrame.Navigate(typeof(HomePage));

        CreateTrayIcon();

        AppWindow.Closing += OnAppWindowClosing;
    }

    private void CreateTrayIcon()
    {
        try
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico")),
                Text = L.Get("MainWindow.TrayTooltip"),
                Visible = true,
            };
            _notifyIcon.MouseDown += (_, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left)
                    ShowMainWindow();
            };

            var menu = new Forms.ContextMenuStrip();
            menu.Opening += (_, _) => ApplyMenuTheme(menu);
            menu.Items.Add(L.Get("MainWindow.TrayOpen"), null, (_, _) => ShowMainWindow());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(L.Get("MainWindow.TrayExit"), null, (_, _) => ExitFromTray());
            _notifyIcon.ContextMenuStrip = menu;
        }
        catch
        {
            _notifyIcon = null;
        }
    }

    private void ApplyMenuTheme(Forms.ContextMenuStrip menu)
    {
        var dark = IsAppDark();
        menu.Renderer = dark
            ? new Forms.ToolStripProfessionalRenderer(new DarkColorTable())
            : new Forms.ToolStripProfessionalRenderer();
        var textColor = dark ? Color.White : Color.Black;
        menu.ForeColor = textColor;
        foreach (Forms.ToolStripItem item in menu.Items)
            item.ForeColor = textColor;
    }

    private static bool IsAppDark()
    {
        if (Application.Current.RequestedTheme == ApplicationTheme.Dark) return true;
        if (Application.Current.RequestedTheme == ApplicationTheme.Light) return false;
        return IsSystemDark();
    }

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    private sealed class DarkColorTable : Forms.ProfessionalColorTable
    {
        private static readonly Color Bg = Color.FromArgb(0x2B, 0x2B, 0x2B);
        private static readonly Color HoverBg = Color.FromArgb(0x41, 0x41, 0x41);
        private static readonly Color HoverBorder = Color.FromArgb(0x55, 0x55, 0x55);

        public override Color ToolStripDropDownBackground => Bg;
        public override Color ToolStripGradientBegin => Bg;
        public override Color ToolStripGradientMiddle => Bg;
        public override Color ToolStripGradientEnd => Bg;
        public override Color ImageMarginGradientBegin => Bg;
        public override Color ImageMarginGradientMiddle => Bg;
        public override Color ImageMarginGradientEnd => Bg;
        public override Color MenuBorder => HoverBorder;
        public override Color MenuItemBorder => HoverBorder;
        public override Color MenuItemSelected => HoverBg;
        public override Color MenuItemSelectedGradientBegin => HoverBg;
        public override Color MenuItemSelectedGradientEnd => HoverBg;
        public override Color MenuItemPressedGradientBegin => HoverBg;
        public override Color MenuItemPressedGradientMiddle => HoverBg;
        public override Color MenuItemPressedGradientEnd => HoverBg;
        public override Color SeparatorDark => HoverBorder;
        public override Color SeparatorLight => Bg;
        public override Color ButtonSelectedHighlight => HoverBg;
        public override Color ButtonSelectedHighlightBorder => HoverBorder;
        public override Color ButtonSelectedBorder => HoverBorder;
        public override Color ToolStripBorder => HoverBorder;
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_forceExit)
        {
            _notifyIcon?.Dispose();
            _notifyIcon = null;
            return;
        }

        var behavior = "ask";
        try
        {
            behavior = WslcHost.Default.Settings.GetCloseBehaviorAsync().GetAwaiter().GetResult();
        }
        catch
        {
        }

        if (behavior == "tray")
        {
            args.Cancel = true;
            MinimizeToTray();
            return;
        }

        if (behavior == "exit")
        {
            _forceExit = true;
            return;
        }

        args.Cancel = true;
        _ = ShowCloseBehaviorDialogAsync();
    }

    private async Task ShowCloseBehaviorDialogAsync()
    {
        var remember = new CheckBox { Content = L.Get("MainWindow.CloseDialogRemember"), VerticalAlignment = VerticalAlignment.Center };
        var dialog = new ContentDialog
        {
            Title = L.Get("MainWindow.CloseDialogTitle"),
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = L.Get("MainWindow.CloseDialogMessage"), TextWrapping = TextWrapping.Wrap },
                    remember,
                },
            },
            PrimaryButtonText = L.Get("MainWindow.CloseDialogMinimize"),
            SecondaryButtonText = L.Get("MainWindow.CloseDialogExit"),
            CloseButtonText = L.Get("MainWindow.CloseDialogCancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content?.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (remember.IsChecked == true)
                try { await WslcHost.Default.Settings.SetCloseBehaviorAsync("tray"); } catch { }
            MinimizeToTray();
        }
        else if (result == ContentDialogResult.Secondary)
        {
            if (remember.IsChecked == true)
                try { await WslcHost.Default.Settings.SetCloseBehaviorAsync("exit"); } catch { }
            ExitFromTray();
        }
    }

    private void MinimizeToTray()
    {
        AppWindow.Hide();
        try
        {
            var notifyEnabled = WslcHost.Default.Settings.GetMinimizeNotifyEnabledAsync().GetAwaiter().GetResult();
            if (notifyEnabled)
                _notifyIcon?.ShowBalloonTip(1500, "WSLCC", L.Get("MainWindow.MinimizedBalloon"), Forms.ToolTipIcon.Info);
        }
        catch
        {
        }
    }

    private void ShowMainWindow()
    {
        AppWindow.Show();
        SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private void ExitFromTray()
    {
        _forceExit = true;
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        Close();
    }

    public void ExitForUpdate()
    {
        _forceExit = true;
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        Close();
    }

    public void ApplyBackdrop(bool enableMica)
        => SystemBackdrop = enableMica ? new MicaBackdrop() : null;

    private async Task InitializeAppearanceAsync()
    {
        try
        {
            var enabled = await WslcHost.Default.Settings.GetMicaEnabledAsync();
            ApplyBackdrop(enabled);
            var theme = await WslcHost.Default.Settings.GetThemeAsync();
            global::WSLCC_App.App.ApplyTheme(theme);
        }
        catch
        {
        }
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    public void NavigateToLogs(string containerName)
        => NavFrame.Navigate(typeof(LogsPage), containerName);

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        NavFrame.GoBack();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage));
            return;
        }
        if (args.SelectedItem is not NavigationViewItem item) return;
        switch (item.Tag)
        {
            case "home":
                NavFrame.Navigate(typeof(HomePage));
                break;
            case "containers":
                NavFrame.Navigate(typeof(ContainersPage));
                break;
            case "compose":
                NavFrame.Navigate(typeof(ComposePage));
                break;
            case "images":
                NavFrame.Navigate(typeof(ImagesPage));
                break;
            case "volumes":
                NavFrame.Navigate(typeof(VolumesPage));
                break;
            case "logs":
                NavFrame.Navigate(typeof(LogsPage));
                break;
            case "activity":
                NavFrame.Navigate(typeof(ActivityPage));
                break;
            case "about":
                NavFrame.Navigate(typeof(AboutPage));
                break;
            default:
                throw new InvalidOperationException($"Unknown navigation item tag: {item.Tag}");
        }
    }
}
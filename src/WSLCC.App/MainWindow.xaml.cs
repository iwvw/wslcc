using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WSLCC.App.Mini;
using WSLCC.App.Pages;
using WSLCC.Core.Services;
using Forms = System.Windows.Forms;

namespace WSLCC_App;

public sealed partial class MainWindow : Window
{
    private Forms.NotifyIcon? _notifyIcon;
    private MiniWindow? _miniWindow;
    private bool _trayMiniPanelEnabled = true;
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
        _ = RefreshTrayPreferencesAsync();

        Activated += (_, _) => RestorePageContent();
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
                    OnTrayLeftClick();
            };

            var menu = new Forms.ContextMenuStrip();
            menu.Opening += (_, _) => ApplyMenuTheme(menu);
            menu.Items.Add(L.Get("MainWindow.TrayOpen"), null, (_, _) => ShowMainWindow());
            menu.Items.Add(L.Get("MainWindow.TrayMiniPanel"), null, (_, _) => ToggleMiniWindow());
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
            CloseMiniWindow();
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
        ReleasePageContent();
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

    private Type? _lastPageType;

    private void CaptureCurrentPage()
    {
        if (NavFrame.Content is not null)
            _lastPageType = NavFrame.Content.GetType();
    }

    private void ReleasePageContent()
    {
        try
        {
            CaptureCurrentPage();
            NavFrame.Content = null;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: false);
            TrimWorkingSet();
        }
        catch (Exception ex)
        {
            App.WriteLog($"释放页面内容失败：{ex}");
        }
    }

    private static void TrimWorkingSet()
    {
        try
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            _ = EmptyWorkingSet(process.Handle);
        }
        catch
        {
        }
    }

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    private void RestorePageContent()
    {
        if (NavFrame.Content is not null || _lastPageType is null) return;
        try
        {
            NavFrame.Navigate(_lastPageType);
        }
        catch (Exception ex)
        {
            App.WriteLog($"恢复页面内容失败：{ex}");
            NavFrame.Navigate(typeof(HomePage));
        }
    }

    private void OnTrayLeftClick()
    {
        if (_trayMiniPanelEnabled) ToggleMiniWindow();
        else ShowMainWindow();
    }

    public async Task RefreshTrayPreferencesAsync()
    {
        try
        {
            _trayMiniPanelEnabled = await WslcHost.Default.Settings.GetTrayMiniPanelEnabledAsync();
        }
        catch (Exception ex)
        {
            App.WriteLog($"读取托盘偏好失败：{ex}");
        }
    }

    public void SetTrayMiniPanelEnabled(bool enabled) => _trayMiniPanelEnabled = enabled;

    public void StartMinimizedToTray() => MinimizeToTray();

    private void ToggleMiniWindow()
    {
        EnsureMiniWindow();
        _miniWindow!.ToggleVisible();
    }

    private void EnsureMiniWindow()
    {
        if (_miniWindow is not null) return;
        var mini = new MiniWindow();
        mini.Dismissed += (_, _) => _miniWindow = null;
        _miniWindow = mini;
    }

    public void ApplyMiniPanelTheme(string theme)
        => _miniWindow?.ApplyTheme(theme);

    public void ShowAndActivate()
    {
        RestorePageContent();
        AppWindow.Show();
        SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
    }

    public void ShowContainers()
    {
        SelectNavItem("containers");
        ShowAndActivate();
    }

    private void SelectNavItem(string tag)
    {
        foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
        {
            if (item.Tag is string value && value == tag)
            {
                NavView.SelectedItem = item;
                return;
            }
        }
    }

    private void ShowMainWindow() => ShowAndActivate();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private void ExitFromTray()
    {
        _forceExit = true;
        CloseMiniWindow();
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        Close();
    }

    public void ExitForUpdate()
    {
        _forceExit = true;
        CloseMiniWindow();
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        Close();
    }

    private void CloseMiniWindow()
    {
        if (_miniWindow is null) return;
        _miniWindow.ForceClose();
        _miniWindow = null;
    }

    public void ApplyBackdrop(bool enableMica)
    {
        SystemBackdrop = enableMica ? new MicaBackdrop() : null;
        _miniWindow?.ApplyBackdrop(enableMica);
    }

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

    public void NavigateToLogs(string containerName)
        => NavFrame.Navigate(typeof(LogsPage), containerName);

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
            case "networks":
                NavFrame.Navigate(typeof(NetworksPage));
                break;
            case "logs":
                NavFrame.Navigate(typeof(LogsPage));
                break;
            case "events":
                NavFrame.Navigate(typeof(EventsPage));
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
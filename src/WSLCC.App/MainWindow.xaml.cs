using System.Runtime.InteropServices;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WSLCC.App.Mini;
using WSLCC.App.Pages;
using WSLCC.App.Services;
using WSLCC.Core.Services;

namespace WSLCC_App;

public sealed partial class MainWindow : Window
{
    private readonly TrayIconService _tray = new();
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
            _tray.LeftClickRequested += OnTrayLeftClick;
            _tray.OpenRequested += ShowMainWindow;
            _tray.MiniPanelRequested += ToggleMiniWindow;
            _tray.SettingsRequested += ShowSettings;
            _tray.ExitRequested += ExitFromTray;
            _tray.ApplyTheme(ToElementTheme(App.CurrentTheme));
            _tray.Show(true);
        }
        catch (Exception ex)
        {
            App.WriteLog($"创建托盘图标失败：{ex}");
        }
    }

    internal static ElementTheme ToElementTheme(string theme) => theme switch
    {
        "dark" => ElementTheme.Dark,
        "light" => ElementTheme.Light,
        _ => ElementTheme.Default,
    };

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_forceExit)
        {
            _tray.Dispose();
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
                _tray.ShowBalloon("WSLCC", L.Get("MainWindow.MinimizedBalloon"));
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

    private void ShowSettings()
    {
        RestorePageContent();
        NavFrame.Navigate(typeof(SettingsPage));
        ShowAndActivate();
    }

    public void ApplyTrayTheme(string theme) => _tray.ApplyTheme(ToElementTheme(theme));

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private void ExitFromTray()
    {
        _forceExit = true;
        CloseMiniWindow();
        _tray.Dispose();
        Close();
    }

    public void ExitForUpdate()
    {
        _forceExit = true;
        CloseMiniWindow();
        _tray.Dispose();
        Close();
    }

    private void CloseMiniWindow()
    {
        if (_miniWindow is null) return;
        _miniWindow.ForceClose();
        _miniWindow = null;
    }

    /// <summary>背景材质：0=亚克力，1=Mica，2=纯色。不支持的会回退。</summary>
    public void ApplyBackdropStyle(int style)
    {
        try
        {
            switch (style)
            {
                case 1 when Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported():
                    SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
                    break;
                case 2:
                    SystemBackdrop = null;
                    break;
                default:
                    if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
                        SystemBackdrop = new AlwaysActiveAcrylicBackdrop();
                    else if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
                        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
                    else
                        SystemBackdrop = null;
                    break;
            }
        }
        catch
        {
        }
        _miniWindow?.ApplyBackdropStyle(style);
    }

    private async Task InitializeAppearanceAsync()
    {
        try
        {
            ApplyBackdropStyle(await WslcHost.Default.Settings.GetBackdropStyleAsync());
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
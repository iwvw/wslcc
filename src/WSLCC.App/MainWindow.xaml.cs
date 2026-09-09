using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WSLCC.App.Pages;
using WSLCC.Core.Services;

namespace WSLCC_App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

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
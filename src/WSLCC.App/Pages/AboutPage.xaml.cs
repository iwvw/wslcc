using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WSLCC.Core.Services;
using WSLCC_App;

namespace WSLCC.App.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = L.GetFormat("About.Version", GetVersion());
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AppLogoImage.Source = LoadAssetImage("Assets", "AppIcon.png");
        AvatarDsuk.Source = LoadAssetImage("Assets", "Avatars", "iwvw.png");
        AvatarLemon.Source = LoadAssetImage("Assets", "Avatars", "lemonno2333.png");

        if (WslcUpdateService.LastResult is { } cached)
            ApplyUpdateBanner(cached);
    }

    private static BitmapImage? LoadAssetImage(params string[] relativeParts)
    {
        try
        {
            var parts = new[] { AppContext.BaseDirectory }.Concat(relativeParts).ToArray();
            var path = Path.Combine(parts);
            return File.Exists(path) ? new BitmapImage(new Uri(path)) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetVersion()
    {
        try
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "-" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
        catch
        {
            return "-";
        }
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = L.Get("About.Checking");
        var info = await WslcHost.Default.Update.CheckAsync();
        ApplyUpdateBanner(info);
        CheckUpdateButton.IsEnabled = true;
    }

    private void ApplyUpdateBanner(UpdateInfo info)
    {
        if (info.Error is not null)
        {
            UpdateStatusText.Text = L.GetFormat("About.CheckFailed", info.Error);
            UpdateActionsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        LatestVersionText.Text = info.LatestVersion ?? "-";
        if (info.HasUpdate)
        {
            UpdateStatusText.Text = L.GetFormat("About.NewVersionFound", info.LatestVersion);
            UpdateActionsPanel.Visibility = Visibility.Visible;
            if (info.ReleaseUrl is not null)
                ReleaseLink.NavigateUri = new Uri(info.ReleaseUrl);
            return;
        }

        UpdateActionsPanel.Visibility = Visibility.Collapsed;
        UpdateStatusText.Text = info.LatestVersion is null
            ? L.Get("About.NoReleaseInfo")
            : L.GetFormat("About.UpToDate", info.LatestVersion);
    }

    private async void DownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        var info = WslcUpdateService.LastResult;
        if (info is null || !info.HasUpdate)
        {
            UpdateStatusText.Text = L.Get("About.CheckUpdateFirst");
            return;
        }

        var confirm = new ContentDialog
        {
            Title = L.Get("About.DownloadAndUpdate"),
            Content = L.GetFormat("About.DownloadConfirm", info.LatestVersion),
            PrimaryButtonText = L.Get("About.DownloadAndUpdate"),
            CloseButtonText = L.Get("About.Cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        DownloadUpdateBtn.IsEnabled = false;
        UpdateStatusText.Text = L.Get("About.Downloading");

        var progress = new Progress<double>(p => UpdateStatusText.Text = L.GetFormat("About.DownloadingPercent", p * 100));
        var result = await WslcHost.Default.Update.DownloadAndInstallAsync(info.DownloadUrl, progress);

        if (result.Error is not null)
        {
            UpdateStatusText.Text = result.Error;
            DownloadUpdateBtn.IsEnabled = true;
            return;
        }

        UpdateStatusText.Text = L.Get("About.InstallerLaunched");
        await Task.Delay(500);
        global::WSLCC_App.App.Main?.ExitForUpdate();
    }
}
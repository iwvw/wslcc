using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WSLCC.Core.Services;

namespace WSLCC.App.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = $"版本 {GetVersion()}";
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
        UpdateStatusText.Text = "检查中…";
        var info = await WslcHost.Default.Update.CheckAsync();
        ApplyUpdateBanner(info);
        CheckUpdateButton.IsEnabled = true;
    }

    private void ApplyUpdateBanner(UpdateInfo info)
    {
        if (info.Error is not null)
        {
            UpdateStatusText.Text = $"检查失败：{info.Error}";
            UpdateBar.IsOpen = false;
            return;
        }

        if (info.HasUpdate)
        {
            UpdateStatusText.Text = $"发现新版本 v{info.LatestVersion}";
            UpdateBar.IsOpen = true;
            if (info.ReleaseUrl is not null)
                ReleaseLink.NavigateUri = new Uri(info.ReleaseUrl);
            return;
        }

        UpdateBar.IsOpen = false;
        UpdateStatusText.Text = info.LatestVersion is null
            ? "暂无发布版本信息"
            : $"已是最新版本（v{info.LatestVersion}）";
    }

    private async void DownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        var info = WslcUpdateService.LastResult;
        if (info is null || !info.HasUpdate)
        {
            UpdateStatusText.Text = "请先点击“检查更新”获取最新版本信息。";
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "下载并更新",
            Content = $"将下载 v{info.LatestVersion} 安装包并启动安装程序，过程中应用会退出，安装完成后可重新打开 WSLCC。",
            PrimaryButtonText = "下载并更新",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        DownloadUpdateBtn.IsEnabled = false;
        UpdateStatusText.Text = "正在下载安装包…";

        var progress = new Progress<double>(p => UpdateStatusText.Text = $"正在下载安装包… {p * 100:0}%");
        var result = await WslcHost.Default.Update.DownloadAndInstallAsync(info.DownloadUrl, progress);

        if (result.Error is not null)
        {
            UpdateStatusText.Text = result.Error;
            DownloadUpdateBtn.IsEnabled = true;
            return;
        }

        UpdateStatusText.Text = "安装程序已启动，应用即将退出…";
        await Task.Delay(500);
        global::WSLCC_App.App.Main?.ExitForUpdate();
    }
}
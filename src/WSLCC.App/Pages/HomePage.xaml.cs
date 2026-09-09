using Microsoft.UI.Xaml.Controls;
using WSLCC.App.ViewModels;
using WSLCC.Core.Services;

namespace WSLCC.App.Pages;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }

    public HomePage()
    {
        InitializeComponent();
        ViewModel = new HomeViewModel(WslcHost.Default);
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    private async void TerminateSessions_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var confirm = new ContentDialog
        {
            Title = "终止会话",
            Content = "将终止所有活动的 wslc 会话，相关 VM 会被关闭。确定继续吗？",
            PrimaryButtonText = "终止",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.TerminateSessionsAsync();
    }

    private async void RestartSessions_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var confirm = new ContentDialog
        {
            Title = "重启会话",
            Content = "将终止并重新拉起 wslc 会话。确定继续吗？",
            PrimaryButtonText = "重启",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.RestartSessionsAsync();
    }
}
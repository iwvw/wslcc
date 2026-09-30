using Microsoft.UI.Xaml.Controls;
using WSLCC.App.ViewModels;
using WSLCC.Core.Services;
using WSLCC_App;

namespace WSLCC.App.Pages;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }

    public HomePage()
    {
        InitializeComponent();
        ViewModel = new HomeViewModel(WslcHost.Default);
        DataContext = ViewModel;
        IssueReporter.AttachTo(ErrorBar, L.Get("Feedback.Page.Home"), () => ViewModel.ErrorMessage);
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    private void OpenSessionTerminal_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not Microsoft.UI.Xaml.FrameworkElement { Tag: string sessionName } || string.IsNullOrWhiteSpace(sessionName))
            return;
        var target = sessionName.Contains(' ') ? $"\"{sessionName}\"" : sessionName;
        var command = $"wslc --session {target} system session shell";
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("wt.exe", $"-- {command}")
            {
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            global::WSLCC_App.App.WriteLog($"会话终端启动失败：{ex}");
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c start {command}")
                {
                    UseShellExecute = false,
                });
            }
            catch (Exception fallbackEx)
            {
                global::WSLCC_App.App.WriteLog($"cmd 会话终端启动失败：{fallbackEx}");
            }
        }
    }

    private async void TerminateSessions_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var confirm = new ContentDialog
        {
            Title = L.Get("HomePage.TerminateDialogTitle"),
            Content = L.Get("HomePage.TerminateDialogMessage"),
            PrimaryButtonText = L.Get("HomePage.TerminateDialogConfirm"),
            CloseButtonText = L.Get("HomePage.TerminateDialogCancel"),
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
            Title = L.Get("HomePage.RestartDialogTitle"),
            Content = L.Get("HomePage.RestartDialogMessage"),
            PrimaryButtonText = L.Get("HomePage.RestartDialogConfirm"),
            CloseButtonText = L.Get("HomePage.RestartDialogCancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.RestartSessionsAsync();
    }
}
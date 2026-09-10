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
        Loaded += async (_, _) => await ViewModel.LoadAsync();
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
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WSLCC.App.ViewModels;
using WSLCC.Core.Services;
using WSLCC_App;

namespace WSLCC.App.Pages;

public sealed partial class ImagesPage : Page
{
    public ImagesViewModel ViewModel { get; }

    public ImagesPage()
    {
        InitializeComponent();
        ViewModel = new ImagesViewModel(WslcHost.Default.Images);
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    private async void ImageAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ImageItemViewModel image }) return;
        switch (sender is FrameworkElement f ? f.Tag?.ToString() : null)
        {
            case "inspect":
                var json = await ViewModel.InspectAsync(image);
                if (json is null) return;
                var scroll = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 420,
                    Content = new TextBlock
                    {
                        Text = json,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12,
                        IsTextSelectionEnabled = true,
                    },
                };
                await ShowInfoAsync(L.Get("ImagesPage.InspectTitle"), scroll);
                break;
            default:
                var confirm = new ContentDialog
                {
                    Title = L.Get("ImagesPage.DeleteImageTitle"),
                    Content = L.GetFormat("ImagesPage.DeleteImageConfirm", image.Source.FullName),
                    PrimaryButtonText = L.Get("ImagesPage.DeleteAction"),
                    CloseButtonText = L.Get("ImagesPage.CancelButton"),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot,
                };
                if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                    await ViewModel.DeleteImageAsync(image);
                break;
        }
    }

    private async void Prune_Click(object sender, RoutedEventArgs e)
    {
        var confirm = new ContentDialog
        {
            Title = L.Get("Prune.ConfirmTitle"),
            Content = L.Get("Prune.ImagesConfirm"),
            PrimaryButtonText = L.Get("ImagesPage.DeleteAction"),
            CloseButtonText = L.Get("ImagesPage.CancelButton"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        var output = await ViewModel.PruneAsync();
        await ShowInfoAsync(
            L.Get("Prune.ConfirmTitle"),
            string.IsNullOrWhiteSpace(output) ? L.GetFormat("Prune.ResultFormat", 0) : output);
    }

    private async Task ShowInfoAsync(string title, object content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = L.Get("ImagesPage.CancelButton"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
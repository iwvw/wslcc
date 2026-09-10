using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    }
}
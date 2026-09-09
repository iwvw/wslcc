using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WSLCC.App.ViewModels;
using WSLCC.Core.Services;

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
            Title = "删除镜像",
            Content = $"确定删除镜像「{image.Source.FullName}」吗？该操作不可撤销。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.DeleteImageAsync(image);
    }
}
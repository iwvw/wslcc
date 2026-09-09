using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WSLCC.App.ViewModels;
using WSLCC.Core.Services;

namespace WSLCC.App.Pages;

public sealed partial class VolumesPage : Page
{
    public VolumesViewModel ViewModel { get; }

    public VolumesPage()
    {
        InitializeComponent();
        ViewModel = new VolumesViewModel(WslcHost.Default.Volumes);
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    private async void VolumeAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: VolumeItemViewModel volume }) return;
        var confirm = new ContentDialog
        {
            Title = "删除卷",
            Content = $"确定删除数据卷「{volume.Source.Name}」吗？卷内数据将被清除。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.DeleteAsync(volume);
    }
}
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WSLCC.App.ViewModels;
using WSLCC.Core.Services;
using WSLCC_App;

namespace WSLCC.App.Pages;

public sealed partial class VolumesPage : Page
{
    public VolumesViewModel ViewModel { get; }

    public VolumesPage()
    {
        InitializeComponent();
        ViewModel = new VolumesViewModel(WslcHost.Default.Volumes);
        DataContext = ViewModel;
        IssueReporter.AttachTo(ErrorBar, L.Get("Feedback.Page.Volumes"), () => ViewModel.ErrorMessage);
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    private async void VolumeAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: VolumeItemViewModel volume }) return;
        var confirm = new ContentDialog
        {
            Title = L.Get("VolumesPage.DeleteVolumeTitle"),
            Content = L.GetFormat("VolumesPage.DeleteVolumeConfirm", volume.Source.Name),
            PrimaryButtonText = L.Get("VolumesPage.DeleteAction"),
            CloseButtonText = L.Get("VolumesPage.CancelButton"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.DeleteAsync(volume);
    }

    private async void Prune_Click(object sender, RoutedEventArgs e)
    {
        var confirm = new ContentDialog
        {
            Title = L.Get("Prune.ConfirmTitle"),
            Content = L.Get("Prune.VolumesConfirm"),
            PrimaryButtonText = L.Get("VolumesPage.DeleteAction"),
            CloseButtonText = L.Get("VolumesPage.CancelButton"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        var output = await ViewModel.PruneAsync();
        var dialog = new ContentDialog
        {
            Title = L.Get("Prune.ConfirmTitle"),
            Content = string.IsNullOrWhiteSpace(output) ? L.GetFormat("Prune.ResultFormat", 0) : output,
            CloseButtonText = L.Get("VolumesPage.CancelButton"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WSLCC.App.ViewModels;
using WSLCC.Core.Services;
using WSLCC_App;

namespace WSLCC.App.Pages;

public sealed partial class NetworksPage : Page
{
    public NetworksViewModel ViewModel { get; }

    public NetworksPage()
    {
        InitializeComponent();
        ViewModel = new NetworksViewModel(WslcHost.Default.Networks);
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox { Header = L.Get("NetworksPage.CreateNamePlaceholder") };
        var driverBox = new TextBox { Header = L.Get("NetworksPage.CreateDriverPlaceholder") };
        var subnetBox = new TextBox { Header = L.Get("NetworksPage.CreateSubnetPlaceholder") };
        var gatewayBox = new TextBox { Header = L.Get("NetworksPage.CreateGatewayPlaceholder") };
        var ipRangeBox = new TextBox { Header = L.Get("NetworksPage.CreateIpRangePlaceholder") };
        var internalBox = new CheckBox { Content = L.Get("NetworksPage.CreateInternal") };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(nameBox);
        panel.Children.Add(driverBox);
        panel.Children.Add(subnetBox);
        panel.Children.Add(gatewayBox);
        panel.Children.Add(ipRangeBox);
        panel.Children.Add(internalBox);

        var dialog = new ContentDialog
        {
            Title = L.Get("NetworksPage.CreateNetworkTitle"),
            Content = panel,
            PrimaryButtonText = L.Get("NetworksPage.CreateButton"),
            CloseButtonText = L.Get("NetworksPage.CancelButton"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (string.IsNullOrWhiteSpace(nameBox.Text)) return;

        await ViewModel.CreateAsync(
            nameBox.Text.Trim(),
            driverBox.Text,
            subnetBox.Text,
            gatewayBox.Text,
            ipRangeBox.Text,
            internalBox.IsChecked == true);
    }

    private async void Prune_Click(object sender, RoutedEventArgs e)
    {
        var confirm = new ContentDialog
        {
            Title = L.Get("NetworksPage.PruneConfirmTitle"),
            Content = L.Get("NetworksPage.PruneConfirmMessage"),
            PrimaryButtonText = L.Get("NetworksPage.DeleteAction"),
            CloseButtonText = L.Get("NetworksPage.CancelButton"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        var output = await ViewModel.PruneAsync();
        await ShowInfoAsync(
            L.Get("NetworksPage.PruneConfirmTitle"),
            string.IsNullOrWhiteSpace(output) ? L.GetFormat("NetworksPage.PrunedFormat", 0) : output);
    }

    private async void NetworkAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NetworkItemViewModel item }) return;
        switch (sender is FrameworkElement f ? f.Tag?.ToString() : null)
        {
            case "delete":
                var confirm = new ContentDialog
                {
                    Title = L.Get("NetworksPage.DeleteNetworkTitle"),
                    Content = L.GetFormat("NetworksPage.DeleteNetworkConfirm", item.Source.Name),
                    PrimaryButtonText = L.Get("NetworksPage.DeleteAction"),
                    CloseButtonText = L.Get("NetworksPage.CancelButton"),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot,
                };
                if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                    await ViewModel.DeleteAsync(item);
                break;
            case "connect":
                var connectBox = new TextBox
                {
                    Header = L.Get("NetworksPage.ConnectPlaceholder"),
                    PlaceholderText = L.Get("NetworksPage.ConnectPlaceholder"),
                };
                var connectDialog = new ContentDialog
                {
                    Title = L.GetFormat("NetworksPage.ConnectTitle", item.Source.Name),
                    Content = connectBox,
                    PrimaryButtonText = L.Get("NetworksPage.ConnectAction"),
                    CloseButtonText = L.Get("NetworksPage.CancelButton"),
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot,
                };
                if (await connectDialog.ShowAsync() == ContentDialogResult.Primary
                    && !string.IsNullOrWhiteSpace(connectBox.Text))
                    await ViewModel.ConnectAsync(item.Source.Name, connectBox.Text.Trim());
                break;
            case "disconnect":
                var disconnectBox = new TextBox
                {
                    Header = L.Get("NetworksPage.ConnectPlaceholder"),
                    PlaceholderText = L.Get("NetworksPage.ConnectPlaceholder"),
                };
                var disconnectDialog = new ContentDialog
                {
                    Title = L.GetFormat("NetworksPage.DisconnectAction", item.Source.Name),
                    Content = disconnectBox,
                    PrimaryButtonText = L.Get("NetworksPage.DisconnectAction"),
                    CloseButtonText = L.Get("NetworksPage.CancelButton"),
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot,
                };
                if (await disconnectDialog.ShowAsync() == ContentDialogResult.Primary
                    && !string.IsNullOrWhiteSpace(disconnectBox.Text))
                    await ViewModel.DisconnectAsync(item.Source.Name, disconnectBox.Text.Trim());
                break;
            case "inspect":
                var json = await ViewModel.InspectAsync(item.Source.Name);
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
                await ShowInfoAsync(L.Get("NetworksPage.InspectTitle"), scroll);
                break;
        }
    }

    private async Task ShowInfoAsync(string title, object content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = L.Get("NetworksPage.CancelButton"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }
}

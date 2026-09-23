using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WSLCC.App.ViewModels;
using WSLCC.Core.Services;
using WSLCC_App;

namespace WSLCC.App.Mini;

public sealed partial class MiniPanelView : UserControl
{
    public MiniPanelViewModel ViewModel { get; }

    public string Title { get; }

    public string EmptyText { get; }

    public string RefreshTooltip { get; }

    public string OpenFullText { get; }

    public event EventHandler? OpenFullRequested;

    public MiniPanelView()
    {
        ViewModel = new MiniPanelViewModel(WslcHost.Default.Containers);
        Title = L.Get("MiniPanel.Title");
        EmptyText = L.Get("MiniPanel.Empty");
        RefreshTooltip = L.Get("MiniPanel.Refresh");
        OpenFullText = L.Get("MiniPanel.OpenFull");
        InitializeComponent();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MiniPanelViewModel.IsRefreshing))
                OnIsRefreshingChanged();
        };
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
        => await ViewModel.LoadAsync();

    private void OnIsRefreshingChanged()
    {
        if (ViewModel.IsRefreshing) RefreshSpin.Begin();
        else RefreshSpin.Stop();
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ContainerItemViewModel item })
            await ViewModel.StartAsync(item);
    }
    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ContainerItemViewModel item })
            await ViewModel.StopAsync(item);
    }

    private async void Restart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ContainerItemViewModel item })
            await ViewModel.RestartAsync(item);
    }

    private async void OpenWeb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ContainerItemViewModel { WebUrl: not null } item })
            await Windows.System.Launcher.LaunchUriAsync(new Uri(item.WebUrl));
    }

    private void OpenFull_Click(object sender, RoutedEventArgs e)
        => OpenFullRequested?.Invoke(this, EventArgs.Empty);

    public void SetSolidBackground(bool solid)
        => SolidBackdrop.Visibility = solid ? Visibility.Visible : Visibility.Collapsed;
}

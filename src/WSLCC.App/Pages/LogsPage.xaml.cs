using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WSLCC.App.ViewModels;
using WSLCC.Core.Services;

namespace WSLCC.App.Pages;

public sealed partial class LogsPage : Page
{
    private string? _pendingContainer;

    public LogsViewModel ViewModel { get; }

    public LogsPage()
    {
        InitializeComponent();
        ViewModel = new LogsViewModel(WslcHost.Default.Logs, WslcHost.Default.Containers);
        DataContext = ViewModel;
        Loaded += async (_, _) =>
        {
            await ViewModel.LoadAsync();
            if (_pendingContainer is { Length: > 0 })
            {
                ViewModel.SelectedContainerName = _pendingContainer;
                _pendingContainer = null;
            }
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string containerName && containerName.Length > 0)
            _pendingContainer = containerName;
    }
}
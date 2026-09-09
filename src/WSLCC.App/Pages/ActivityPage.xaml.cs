using Microsoft.UI.Xaml.Controls;
using WSLCC.App.ViewModels;
using WSLCC.Core.Services;

namespace WSLCC.App.Pages;

public sealed partial class ActivityPage : Page
{
    public ActivityViewModel ViewModel { get; }

    public ActivityPage()
    {
        InitializeComponent();
        ViewModel = new ActivityViewModel(WslcHost.Default);
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }
}
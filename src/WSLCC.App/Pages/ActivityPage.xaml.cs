using Microsoft.UI.Xaml.Controls;
using WSLCC.App.ViewModels;
using WSLCC.Core.Services;
using WSLCC_App;

namespace WSLCC.App.Pages;

public sealed partial class ActivityPage : Page
{
    public ActivityViewModel ViewModel { get; }

    public ActivityPage()
    {
        InitializeComponent();
        ViewModel = new ActivityViewModel(WslcHost.Default);
        DataContext = ViewModel;
        IssueReporter.AttachTo(ErrorBar, L.Get("Feedback.Page.Activity"), () => ViewModel.ErrorMessage);
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }
}
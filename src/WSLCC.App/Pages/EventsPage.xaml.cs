using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WSLCC.App.ViewModels;
using WSLCC.Core.Services;
using WSLCC_App;

namespace WSLCC.App.Pages;

public sealed partial class EventsPage : Page
{
    public EventsViewModel ViewModel { get; }

    public EventsPage()
    {
        InitializeComponent();
        ViewModel = new EventsViewModel(WslcHost.Default.Events);
        DataContext = ViewModel;
        IssueReporter.AttachTo(ErrorBar, L.Get("Feedback.Page.Events"), () => ViewModel.ErrorMessage);
        Loaded += async (_, _) => await ViewModel.InitializeAsync();
        Unloaded += (_, _) => ViewModel.StopStream();
    }
}

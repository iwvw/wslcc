using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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

    public string SessionStartTooltip { get; }

    public string SessionStopTooltip { get; }

    public event EventHandler? OpenFullRequested;

    public event EventHandler? CollapseRequested;

    public event EventHandler? DragGestureStarted;

    public event EventHandler? DragGestureMoved;

    public event EventHandler? DragGestureEnded;

    private const double DragThresholdDip = 8;

    private bool _dragArmed;
    private bool _dragActive;
    private uint? _dragPointerId;
    private Windows.Foundation.Point _pressPoint;

    public MiniPanelView()
    {
        ViewModel = new MiniPanelViewModel(WslcHost.Default);
        Title = L.Get("MiniPanel.Title");
        EmptyText = L.Get("MiniPanel.Empty");
        RefreshTooltip = L.Get("MiniPanel.Refresh");
        OpenFullText = L.Get("MiniPanel.OpenFull");
        SessionStartTooltip = L.Get("MiniPanel.SessionStart");
        SessionStopTooltip = L.Get("MiniPanel.SessionStop");
        InitializeComponent();

        RootGrid.AddHandler(PointerPressedEvent, new PointerEventHandler(OnRootPointerPressed), handledEventsToo: true);
        RootGrid.AddHandler(PointerMovedEvent, new PointerEventHandler(OnRootPointerMoved), handledEventsToo: true);
        RootGrid.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnRootPointerReleased), handledEventsToo: true);
        RootGrid.AddHandler(PointerCanceledEvent, new PointerEventHandler(OnRootPointerCanceled), handledEventsToo: true);
    }

    private void OnRootPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_dragArmed || _dragActive) return;
        if (!e.GetCurrentPoint(RootGrid).Properties.IsLeftButtonPressed) return;
        _dragArmed = true;
        _dragPointerId = e.Pointer.PointerId;
        _pressPoint = e.GetCurrentPoint(RootGrid).Position;
    }

    private void OnRootPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerId != _dragPointerId) return;
        if (_dragActive)
        {
            DragGestureMoved?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (!_dragArmed) return;

        var pos = e.GetCurrentPoint(RootGrid).Position;
        double dx = pos.X - _pressPoint.X;
        double dy = pos.Y - _pressPoint.Y;
        if (Math.Abs(dx) <= DragThresholdDip && Math.Abs(dy) <= DragThresholdDip) return;

        // 位移超阈值：从轻点转为拖拽，接管指针，后续事件只发给本控件。
        _dragArmed = false;
        _dragActive = true;
        RootGrid.CapturePointer(e.Pointer);
        DragGestureStarted?.Invoke(this, EventArgs.Empty);
        DragGestureMoved?.Invoke(this, EventArgs.Empty);
    }

    private void OnRootPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerId != _dragPointerId) return;
        if (_dragActive)
        {
            _dragActive = false;
            _dragArmed = false;
            _dragPointerId = null;
            RootGrid.ReleasePointerCapture(e.Pointer);
            DragGestureEnded?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            // 未进入拖拽即视为轻点：不接管，让控件（按钮等）正常收到 Click。
            _dragArmed = false;
            _dragPointerId = null;
        }
    }

    private void OnRootPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerId != _dragPointerId) return;
        bool wasActive = _dragActive;
        _dragActive = false;
        _dragArmed = false;
        _dragPointerId = null;
        if (wasActive) DragGestureEnded?.Invoke(this, EventArgs.Empty);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
        => await ViewModel.LoadAsync();

    private async void SessionToggle_Click(object sender, RoutedEventArgs e)
        => await ViewModel.ToggleSessionAsync();

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

    private void Collapse_Click(object sender, RoutedEventArgs e)
        => CollapseRequested?.Invoke(this, EventArgs.Empty);

    public void SetSolidBackground(bool solid)
        => SolidBackdrop.Visibility = solid ? Visibility.Visible : Visibility.Collapsed;

    public async Task RefreshOnShowAsync() => await ViewModel.LoadAsync();
}

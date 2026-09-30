using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WSLCC.Core.Services;
using WSLCC_App;
using WinRT.Interop;

namespace WSLCC.App.Mini;

public sealed partial class MiniWindow : Window
{
    private const int DesignWidth = 380;
    private const int DesignHeight = 540;
    private const int MarginRight = 12;
    private const int MarginBottom = 12;
    private const int ShowDurationMs = 260;
    private const int HideDurationMs = 220;
    private const double DragDismissThresholdRatio = 0.28;

    private enum VisState
    {
        Hidden,
        Showing,
        Shown,
        Hiding,
    }

    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly WindowSlider _slider;
    private readonly DispatcherTimer _statsTimer;
    private bool _forceClose;
    private VisState _state = VisState.Hidden;
    private Windows.Graphics.RectInt32 _targetRect;

    private bool _dragging;
    private Windows.Graphics.RectInt32 _dragStartRect;
    private int _dragStartCursorY;

    /// <summary>拖拽收起关闭时触发，宿主据此清理窗口引用。</summary>
    public event EventHandler? Dismissed;

    public MiniWindow()
    {
        InitializeComponent();

        Title = L.Get("MiniPanel.Title");
        _hwnd = WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd));
        _slider = new WindowSlider(_hwnd, DispatcherQueue);

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        _appWindow.SetPresenter(presenter);
        _appWindow.IsShownInSwitchers = false;

        var micaEnabled = true;
        try
        {
            micaEnabled = WslcHost.Default.Settings.GetMicaEnabledAsync().GetAwaiter().GetResult();
        }
        catch
        {
        }

        ApplyBackdrop(micaEnabled);

        WindowChrome.SetToolWindow(_hwnd);
        WindowChrome.SetRoundCorner(_hwnd);
        WindowChrome.RemoveNonClientFrame(_hwnd);
        WindowChrome.SetTopmost(_hwnd);
        WindowChrome.SetWindowAlpha(_hwnd, 255);

        _targetRect = DpiLayout.ComputeBottomRight(_appWindow, DesignWidth, DesignHeight, MarginRight, MarginBottom);
        _appWindow.MoveAndResize(_targetRect);

        _appWindow.Closing += OnClosing;

        Panel.OpenFullRequested += (_, _) => OpenFullPanel();
        Panel.CollapseRequested += (_, _) => Hide();
        Panel.DragGestureStarted += (_, _) => OnDragStarted();
        Panel.DragGestureMoved += (_, _) => OnDragMoved();
        Panel.DragGestureEnded += (_, _) => OnDragEnded();
        ApplyTheme(global::WSLCC_App.App.CurrentTheme);

        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _statsTimer.Tick += async (_, _) => await Panel.ViewModel.RefreshStatsAsync();
    }

    public void ApplyTheme(string theme)
    {
        Panel.RequestedTheme = theme switch
        {
            "dark" => ElementTheme.Dark,
            "light" => ElementTheme.Light,
            _ => ElementTheme.Default,
        };
    }

    public void ApplyBackdrop(bool acrylic)
    {
        SystemBackdrop = acrylic ? new AlwaysActiveAcrylicBackdrop() : null;
        Panel.SetSolidBackground(!acrylic);
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_forceClose) return;
        args.Cancel = true;
        Hide();
    }

    public void ForceClose()
    {
        _forceClose = true;
        _statsTimer.Stop();
        _slider.Dispose();
        Close();
    }

    public bool IsVisible => _state is VisState.Showing or VisState.Shown;

    public void ToggleVisible()
    {
        if (IsVisible) Hide();
        else Show();
    }

    public void Show()
    {
        bool wasHidden = _state == VisState.Hidden;
        _state = VisState.Showing;

        _targetRect = DpiLayout.ComputeBottomRight(_appWindow, DesignWidth, DesignHeight, MarginRight, MarginBottom);
        var hiddenRect = SlideMath.Hidden(_targetRect, SlideDirection.BottomUp);

        Windows.Graphics.RectInt32 start = hiddenRect;
        if (!wasHidden)
        {
            var current = DpiLayout.GetCurrentRect(_hwnd);
            if (current.Width > 0 && current.Height > 0) start = current;
        }
        else
        {
            _appWindow.MoveAndResize(start);
        }

        WindowChrome.SetWindowAlpha(_hwnd, 255);
        _appWindow.Show();
        WindowChrome.BringToForeground(_hwnd);
        // 必须最后插到任务栏下方：任务栏遮住窗口，滑动时才有从任务栏后钻出的观感。
        WindowChrome.PlaceBelowTaskbar(_hwnd);

        // 呼出：fast-out / slow-in，末尾缓缓减速，对齐 Fluent 展开曲线。
        _slider.Animate(start, _targetRect, 255, 255, ShowDurationMs, SlideEasing.EaseOut, OnShowCompleted);

        _statsTimer.Start();
        _ = Panel.ViewModel.LoadAsync();
    }

    private void OnShowCompleted()
    {
        if (_state != VisState.Showing) return;
        _state = VisState.Shown;
    }

    public void Hide()
    {
        if (_state is VisState.Hidden or VisState.Hiding) return;
        _state = VisState.Hiding;

        _statsTimer.Stop();

        _targetRect = DpiLayout.ComputeBottomRight(_appWindow, DesignWidth, DesignHeight, MarginRight, MarginBottom);
        var hiddenRect = SlideMath.Hidden(_targetRect, SlideDirection.BottomUp);

        var current = DpiLayout.GetCurrentRect(_hwnd);
        if (current.Width <= 0 || current.Height <= 0) current = _targetRect;

        // 收起前把窗口插回任务栏下方：向下滑时被任务栏遮住，产生从任务栏后藏入的观感。
        WindowChrome.PlaceBelowTaskbar(_hwnd);

        // 收起：反向曲线，开始缓缓起步，对齐 Fluent 退出动画。
        _slider.Animate(current, hiddenRect, 255, 255, HideDurationMs, SlideEasing.EaseIn, OnHideCompleted);
    }

    private void OnHideCompleted()
    {
        if (_state != VisState.Hiding) return;
        _appWindow.Hide();
        _appWindow.MoveAndResize(_targetRect);
        WindowChrome.SetWindowAlpha(_hwnd, 255);
        _state = VisState.Hidden;
    }

    // ---- 下拉关闭手势：按住空白处往下拖，跟手移动；超阈值下滑收起并关闭，否则回弹 ----

    private void OnDragStarted()
    {
        if (_state != VisState.Shown) return;
        _slider.Cancel();
        WindowChrome.PlaceBelowTaskbar(_hwnd);
        _dragging = true;
        _dragStartRect = DpiLayout.GetCurrentRect(_hwnd);
        GetCursorPos(out var p);
        _dragStartCursorY = p.Y;
    }

    private void OnDragMoved()
    {
        if (!_dragging) return;
        GetCursorPos(out var p);
        int dy = p.Y - _dragStartCursorY;
        // 跟手：窗口随鼠标移动；钳制在起始位置之下，避免拖出屏幕顶部（可往回拖到原位取消）。
        int y = Math.Max(_dragStartRect.Y, _dragStartRect.Y + dy);
        _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(
            _dragStartRect.X, y, _dragStartRect.Width, _dragStartRect.Height));
    }

    private void OnDragEnded()
    {
        if (!_dragging) return;
        _dragging = false;
        GetCursorPos(out var p);
        int dy = p.Y - _dragStartCursorY;
        var current = DpiLayout.GetCurrentRect(_hwnd);

        if (dy >= _dragStartRect.Height * DragDismissThresholdRatio)
            DismissFromDrag(current);
        else
            SnapBackFromDrag(current);
    }

    private void DismissFromDrag(Windows.Graphics.RectInt32 current)
    {
        _statsTimer.Stop();
        WindowChrome.PlaceBelowTaskbar(_hwnd);
        var hidden = SlideMath.Hidden(current, SlideDirection.BottomUp);
        _slider.Animate(current, hidden, 255, 255, 240, SlideEasing.EaseIn, () =>
        {
            _state = VisState.Hidden;
            _appWindow.Hide();
            _appWindow.MoveAndResize(_targetRect);
            _forceClose = true;
            Dismissed?.Invoke(this, EventArgs.Empty);
            _slider.Dispose();
            Close();
        });
    }

    private void SnapBackFromDrag(Windows.Graphics.RectInt32 current)
        => _slider.Animate(current, _targetRect, 255, 255, 220, SlideEasing.EaseOut, null);

    private void OpenFullPanel()
    {
        Hide();
        global::WSLCC_App.App.Main?.ShowContainers();
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);
}

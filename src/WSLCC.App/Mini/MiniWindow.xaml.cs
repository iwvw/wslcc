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
    private const int ShowDurationMs = 240;
    private const int HideDurationMs = 220;

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

    public void ToggleVisible()
    {
        if (_state is VisState.Showing or VisState.Shown) Hide();
        else Show();
    }

    public void Show()
    {
        bool wasHidden = _state == VisState.Hidden;
        _state = VisState.Showing;

        _targetRect = DpiLayout.ComputeBottomRight(_appWindow, DesignWidth, DesignHeight, MarginRight, MarginBottom);

        Windows.Graphics.RectInt32 start = SlideMath.Hidden(_targetRect, SlideDirection.BottomUp);
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
        WindowChrome.PlaceBelowTaskbar(_hwnd);
        _slider.Animate(start, _targetRect, 255, 255, ShowDurationMs, easeOut: true, OnShowCompleted);

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

        var current = DpiLayout.GetCurrentRect(_hwnd);
        if (current.Width <= 0 || current.Height <= 0) current = _targetRect;

        int currentAlpha = WindowChrome.GetWindowAlpha(_hwnd);
        _slider.Animate(current, SlideMath.Hidden(_targetRect, SlideDirection.BottomUp), currentAlpha, 0,
            HideDurationMs, easeOut: true, OnHideCompleted);
    }

    private void OnHideCompleted()
    {
        if (_state != VisState.Hiding) return;
        _appWindow.Hide();
        _appWindow.MoveAndResize(_targetRect);
        WindowChrome.SetWindowAlpha(_hwnd, 255);
        _state = VisState.Hidden;
    }

    private void OpenFullPanel()
    {
        Hide();
        global::WSLCC_App.App.Main?.ShowContainers();
    }
}

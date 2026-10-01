using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WSLCC_App;

namespace WSLCC.App.Services;

internal sealed class TrayIconService : IDisposable
{
    private TaskbarIcon? _icon;
    private ElementTheme _theme = ElementTheme.Default;

    public event Action? LeftClickRequested;
    public event Action? OpenRequested;
    public event Action? MiniPanelRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public void Show(bool visible)
    {
        if (_icon is null)
        {
            if (!visible) return;
            _icon = CreateIcon();
        }

        _icon.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ApplyTheme(ElementTheme theme)
    {
        _theme = theme;
        if (_icon is not null)
        {
            _icon.RequestedTheme = theme;
        }
    }

    public void ShowBalloon(string title, string message)
    {
        _icon?.ShowNotification(title, message);
    }

    private TaskbarIcon CreateIcon()
    {
        var icon = (TaskbarIcon)Application.Current.Resources["WslccTrayIcon"];

        icon.RequestedTheme = _theme;
        icon.ContextMenuMode = ContextMenuMode.SecondWindow;
        icon.ContextMenuThemeMode = PopupMenuThemeMode.System;
        icon.LeftClickCommand = new RelayCommand(() => LeftClickRequested?.Invoke());
        icon.NoLeftClickDelay = true;
        icon.ToolTipText = L.Get("MainWindow.TrayTooltip");

        if (icon.ContextFlyout is MenuFlyout menu)
        {
            Wire(menu, 0, () => OpenRequested?.Invoke());
            Wire(menu, 1, () => MiniPanelRequested?.Invoke());
            Wire(menu, 3, () => SettingsRequested?.Invoke());
            Wire(menu, 5, () => ExitRequested?.Invoke());
            Localize(menu);
        }

        icon.ForceCreate(enablesEfficiencyMode: false);
        return icon;
    }

    private static void Localize(MenuFlyout menu)
    {
        SetText(menu, 0, "MainWindow.TrayOpen");
        SetText(menu, 1, "MainWindow.TrayMiniPanel");
        SetText(menu, 3, "MainWindow.TraySettings");
        SetText(menu, 5, "MainWindow.TrayExit");
    }

    private static void Wire(MenuFlyout menu, int index, Action handler)
    {
        if (index < menu.Items.Count && menu.Items[index] is MenuFlyoutItem item)
        {
            item.Click += (_, _) => handler();
        }
    }

    private static void SetText(MenuFlyout menu, int index, string key)
    {
        if (index < menu.Items.Count && menu.Items[index] is MenuFlyoutItem item)
        {
            item.Text = L.Get(key);
        }
    }

    public void Dispose()
    {
        if (_icon is null) return;
        _icon.Visibility = Visibility.Collapsed;
        _icon.Dispose();
        _icon = null;
    }

    private sealed class RelayCommand(Action execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}

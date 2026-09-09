using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using WSLCC.Core.Services;
using Windows.UI;

namespace WSLCC_App;

public partial class App : Application
{
    private Window? _window;

    public static MainWindow? Main { get; private set; }

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WSLCC", "app-crash.log");

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => WriteLog($"UnhandledException: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => WriteLog($"AppDomain: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) => WriteLog($"UnobservedTask: {e.Exception}");
    }

    private static void WriteLog(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:O}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    public static void ApplyTheme(string theme)
    {
        if (theme == "dark")
            Application.Current.RequestedTheme = ApplicationTheme.Dark;
        else if (theme == "light")
            Application.Current.RequestedTheme = ApplicationTheme.Light;

        var window = Main;
        if (window?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                "dark" => ElementTheme.Dark,
                "light" => ElementTheme.Light,
                _ => ElementTheme.Default,
            };
        }
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            await WslcHost.Default.InitializeAsync();
        }
        catch (Exception ex)
        {
            WriteLog($"InitializeAsync failed: {ex}");
        }
        _window = new MainWindow();
        Main = (MainWindow)_window;
        _window.Activate();
    }
}
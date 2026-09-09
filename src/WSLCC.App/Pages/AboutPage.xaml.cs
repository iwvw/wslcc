using System.Reflection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace WSLCC.App.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = $"版本 {GetVersion()}";
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        AppLogoImage.Source = LoadAssetImage("Assets", "AppIcon.png");
        AvatarDsuk.Source = LoadAssetImage("Assets", "Avatars", "iwvw.png");
        AvatarLemon.Source = LoadAssetImage("Assets", "Avatars", "lemonno2333.png");
    }

    private static BitmapImage? LoadAssetImage(params string[] relativeParts)
    {
        try
        {
            var parts = new[] { AppContext.BaseDirectory }.Concat(relativeParts).ToArray();
            var path = Path.Combine(parts);
            return File.Exists(path) ? new BitmapImage(new Uri(path)) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetVersion()
    {
        try
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "-" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
        catch
        {
            return "-";
        }
    }
}
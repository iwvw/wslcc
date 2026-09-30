using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WSLCC.Core.Cli;
using WSLCC.Core.Services;
using WSLCC_App;

namespace WSLCC.App;

public static class IssueReporter
{
    public static void AttachTo(InfoBar bar, string pageLabel, Func<string?> detail)
    {
        var button = new Button
        {
            Content = L.Get("Common.ReportIssue"),
        };
        button.Click += (_, _) => _ = ShowDialogAsync(bar.XamlRoot, pageLabel, detail());
        bar.ActionButton = button;
    }

    public static async Task ShowDialogAsync(XamlRoot? xamlRoot, string pageLabel, string? detail)
    {
        if (xamlRoot is null) return;
        var context = L.GetFormat("Feedback.ContextFormat", pageLabel);
        var info = await CollectAsync();
        var diagnostics = IssueReportBuilder.BuildDiagnostics(context, detail, info);

        var hint = new TextBlock
        {
            Text = L.Get("Feedback.IssueHint"),
            TextWrapping = TextWrapping.Wrap,
        };
        var detailBlock = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(detail) ? L.Get("Feedback.NoDetail") : detail,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize = 12,
            IsTextSelectionEnabled = true,
        };
        var detailScroll = new ScrollViewer
        {
            MaxHeight = 160,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = detailBlock,
        };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(hint);
        panel.Children.Add(detailScroll);

        var dialog = new ContentDialog
        {
            Title = L.Get("Common.ReportIssueTitle"),
            Content = panel,
            PrimaryButtonText = L.Get("Feedback.CopyDiagnostics"),
            SecondaryButtonText = L.Get("Feedback.OpenGitHub"),
            CloseButtonText = L.Get("Feedback.Close"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            CopyToClipboard(diagnostics);
            await ShowCopiedAsync(xamlRoot);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            await LaunchAsync(context, detail, info);
        }
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        }
        catch (Exception ex)
        {
            global::WSLCC_App.App.WriteLog($"复制诊断信息失败：{ex}");
        }
    }

    private static async Task ShowCopiedAsync(XamlRoot xamlRoot)
    {
        var dialog = new ContentDialog
        {
            Title = L.Get("Feedback.CopiedTitle"),
            Content = new TextBlock
            {
                Text = L.Get("Feedback.CopiedMessage"),
                TextWrapping = TextWrapping.Wrap,
            },
            CloseButtonText = L.Get("Feedback.Close"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
        };
        await dialog.ShowAsync();
    }

    private static async Task LaunchAsync(string context, string? detail, IssueReportContext info)
    {
        try
        {
            var url = IssueReportBuilder.BuildUrl(context, detail, info);
            await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
        }
        catch (Exception ex)
        {
            global::WSLCC_App.App.WriteLog($"打开反馈链接失败：{ex}");
        }
    }

    private static async Task<IssueReportContext> CollectAsync()
    {
        var tun = WslcNetworkDiagnostics.DetectProxyTunInterfaces();
        var audit = await CollectRecentAuditAsync();
        var log = CollectAppLogTail();
        return new IssueReportContext(GetAppVersion(), ResolveWslcVersion(), tun, audit, log);
    }

    private static async Task<IReadOnlyList<string>> CollectRecentAuditAsync()
    {
        try
        {
            var entries = await WslcHost.Default.Audit.QueryAsync(10);
            var lines = new List<string>();
            foreach (var e in entries)
            {
                var message = string.IsNullOrWhiteSpace(e.Message) ? string.Empty : $" | {e.Message}";
                lines.Add($"{e.Timestamp.LocalDateTime:MM-dd HH:mm:ss} [{e.Category}/{e.Action}] {e.Result} {e.Detail}{message}");
            }
            return lines;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string? CollectAppLogTail(int maxLines = 30)
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WSLCC", "app-crash.log");
            if (!File.Exists(path)) return null;
            var lines = File.ReadAllLines(path);
            if (lines.Length == 0) return null;
            var tail = lines.Length <= maxLines ? lines : lines[^maxLines..];
            return string.Join(Environment.NewLine, tail);
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveWslcVersion()
    {
        try
        {
            var output = WslcHost.Default.Runner.RunAsync(
                ["--version"], new WslcRunner.RunOptions(CheckOutputForErrors: false)).GetAwaiter().GetResult();
            return output.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string GetAppVersion()
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

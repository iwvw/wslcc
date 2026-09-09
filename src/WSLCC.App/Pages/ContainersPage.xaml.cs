using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WSLCC.App.ViewModels;
using WSLCC.Core.Models;
using WSLCC.Core.Services;

namespace WSLCC.App.Pages;

public sealed partial class ContainersPage : Page
{
    public ContainersViewModel ViewModel { get; }

    public ContainersPage()
    {
        InitializeComponent();
        ViewModel = new ContainersViewModel(
            WslcHost.Default.Containers, WslcHost.Default.Compose, WslcHost.Default.Settings)
        {
            InspectProvider = ShowInspectDialogAsync,
        };
        DataContext = ViewModel;
        Loaded += async (_, _) =>
        {
            await ViewModel.LoadAsync();
            ViewModel.StartAutoRefresh();
        };
        Unloaded += (_, _) => ViewModel.StopAutoRefresh();
    }

    private async void NewContainer_Click(object sender, RoutedEventArgs e)
        => await ShowCreateDialogAsync();

    private async void ComposeDeploy_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickComposeFileAsync();
        if (path is null) return;

        var services = await ViewModel.ParseComposeAsync(path);
        if (services.Count == 0) return;

        var defaultMirror = await ViewModel.GetRegistryMirrorAsync();
        var confirmPanel = new StackPanel { Spacing = 12 };
        var serviceText = new TextBlock
        {
            Text = string.Join(Environment.NewLine,
                services.Select(s => $"{s.ContainerName}  ←  {s.Image}")),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        confirmPanel.Children.Add(serviceText);
        var mirrorBox = new TextBox
        {
            PlaceholderText = "留空不替换",
            Header = "镜像加速源",
            Text = defaultMirror,
        };
        confirmPanel.Children.Add(mirrorBox);

        var confirm = new ContentDialog
        {
            Title = "Compose 部署确认",
            Content = confirmPanel,
            PrimaryButtonText = "部署",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        var mirror = string.IsNullOrWhiteSpace(mirrorBox.Text) ? null : mirrorBox.Text.Trim();
        await ShowComposeProgressAsync("Compose 部署", async progress =>
        {
            var results = await ViewModel.DeployComposeAsync(path, progress, mirror);
            return string.Join(Environment.NewLine,
                results.Select(r => r.Success
                    ? $"部署成功  {r.ContainerName}  {r.Error}"
                    : $"部署失败  {r.ContainerName}  {r.Error}"));
        });
    }

    private async void ComposeDown_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickComposeFileAsync();
        if (path is null) return;

        var services = await ViewModel.ParseComposeAsync(path);
        if (services.Count == 0) return;

        var confirm = new ContentDialog
        {
            Title = "Compose 停止",
            Content = "将停止并删除以下容器：\n" + string.Join("\n", services.Select(s => $"• {s.ContainerName}")),
            PrimaryButtonText = "停止并删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        await ShowComposeProgressAsync("Compose 停止", async progress =>
        {
            var stopped = await ViewModel.StopComposeAsync(path, progress);
            return string.Join(Environment.NewLine, stopped.Select(s => $"已删除  {s}"));
        });
    }

    private async Task ShowComposeProgressAsync(string title, Func<IProgress<string>, Task<string>> run)
    {
        var statusText = new TextBlock { Text = "执行中..." };
        var dialog = new ContentDialog
        {
            Title = title,
            Content = statusText,
            CloseButtonText = "完成",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        var showTask = dialog.ShowAsync();
        var progress = new Progress<string>(line => statusText.Text = line);
        var summary = await run(progress);
        statusText.Text = summary;
        await showTask;
    }

    private async Task<string?> PickComposeFileAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add(".yml");
        picker.FileTypeFilter.Add(".yaml");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(global::WSLCC_App.App.Main!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async void ContainerAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ContainerItemViewModel item }) return;
        switch (sender is FrameworkElement f ? f.Tag?.ToString() : null)
        {
            case "start":
                await ViewModel.StartContainerAsync(item);
                break;
            case "stop":
                await ViewModel.StopContainerAsync(item);
                break;
            case "restart":
                await ViewModel.RestartContainerAsync(item);
                break;
            case "delete":
                var confirm = new ContentDialog
                {
                    Title = "删除容器",
                    Content = $"确定删除容器「{item.Source.Name}」吗？该操作不可撤销。",
                    PrimaryButtonText = "删除",
                    CloseButtonText = "取消",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot,
                };
                if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                    await ViewModel.DeleteContainerAsync(item);
                break;
            case "logs":
                global::WSLCC_App.App.Main!.NavigateToLogs(item.Source.Name);
                break;
            case "inspect":
                await ShowInspectDialogAsync(item);
                break;
            case "terminal":
                LaunchTerminal(item.Source.Name);
                break;
            case "web":
                if (item.WebUrl is not null)
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(item.WebUrl));
                break;
        }
    }

    private static void LaunchTerminal(string containerName)
    {
        var target = QuoteShell(containerName);
        try
        {
            Process.Start(new ProcessStartInfo("wt.exe", $"-- wslc exec -it {target} /bin/sh") { UseShellExecute = false });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe", $"/c start wslc exec -it {target} /bin/sh") { UseShellExecute = false });
            }
            catch
            {
            }
        }
    }

    private static string QuoteShell(string value)
        => value.Contains(' ') ? $"\"{value}\"" : value;

    private async Task ShowInspectDialogAsync(ContainerItemViewModel container)
    {
        string json;
        try
        {
            json = await WslcHost.Default.Inspect.InspectContainerAsync(container.Source.Name);
        }
        catch (Exception ex)
        {
            await ShowInfoDialogAsync("详情不可用", ex.Message);
            return;
        }
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 420,
        };
        scroll.Content = new TextBlock
        {
            Text = json,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            IsTextSelectionEnabled = true,
        };
        await ShowInfoDialogAsync($"容器详情：{container.Source.Name}", scroll);
    }

    private async Task ShowInfoDialogAsync(string title, object content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private async Task<ContainerCreateOptions?> ShowCreateDialogAsync()
    {
        var nameBox = new TextBox { PlaceholderText = "容器名称（可选）" };
        var imageBox = new TextBox { PlaceholderText = "镜像，如 nginx:latest" };
        var portsBox = new TextBox { PlaceholderText = "端口映射，如 8080:80（多个用逗号分隔）" };
        var envBox = new TextBox { PlaceholderText = "环境变量，如 TZ=Asia/Shanghai（多个用逗号分隔）" };
        var commandBox = new TextBox
        {
            PlaceholderText = "如 /app/api-monitor 或 nginx -g 'daemon off;'（留空使用镜像默认）",
            Header = "启动命令（可选）",
        };
        var volumeBox = new TextBox
        {
            PlaceholderText = @"卷映射，如 C:\data:/app/data 或 ./data:/app/data（多个用逗号分隔）",
            Header = "卷 / 目录映射（可选）",
        };
        var autoRemove = new CheckBox { Content = "退出后自动删除 (--rm)" };
        var detached = new CheckBox { Content = "后台运行 (-d)", IsChecked = true };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(nameBox);
        panel.Children.Add(imageBox);
        panel.Children.Add(portsBox);
        panel.Children.Add(envBox);
        panel.Children.Add(commandBox);
        panel.Children.Add(volumeBox);
        panel.Children.Add(autoRemove);
        panel.Children.Add(detached);

        var dialog = new ContentDialog
        {
            Title = "新建容器",
            Content = panel,
            PrimaryButtonText = "创建",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;
        if (string.IsNullOrWhiteSpace(imageBox.Text)) return null;

        var name = string.IsNullOrWhiteSpace(nameBox.Text)
            ? Guid.NewGuid().ToString("N")[..8]
            : nameBox.Text.Trim();
        var ports = SplitList(portsBox.Text);
        var envs = SplitList(envBox.Text);
        var volumes = SplitList(volumeBox.Text);
        var command = TokenizeCommand(commandBox.Text);

        return new ContainerCreateOptions(
            name,
            imageBox.Text.Trim(),
            ports,
            envs,
            volumes,
            autoRemove.IsChecked == true,
            detached.IsChecked == true,
            Command: command);
    }

    private static string[] SplitList(string text)
        => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<string>? TokenizeCommand(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuote = false;
        foreach (var ch in text.Trim())
        {
            if (ch == '"')
            {
                inQuote = !inQuote;
            }
            else if (ch == '\'' && !inQuote)
            {
                continue;
            }
            else if (ch == ' ' && !inQuote)
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }
        if (current.Length > 0) parts.Add(current.ToString());
        return parts.Count > 0 ? parts : null;
    }
}
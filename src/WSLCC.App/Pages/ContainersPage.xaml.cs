using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WSLCC.App.ViewModels;
using WSLCC.Core.Models;
using WSLCC.Core.Services;
using WSLCC_App;

namespace WSLCC.App.Pages;

public sealed partial class ContainersPage : Page
{
    public ContainersViewModel ViewModel { get; }

    private bool _isActive;

    public ContainersPage()
    {
        InitializeComponent();
        ViewModel = new ContainersViewModel(
            WslcHost.Default.Containers, WslcHost.Default.Compose, WslcHost.Default.Settings,
            WslcHost.Default.History);
        DataContext = ViewModel;
        Loaded += async (_, _) =>
        {
            _isActive = true;
            await ViewModel.LoadAsync();
            if (_isActive) ViewModel.StartAutoRefresh();
        };
        Unloaded += (_, _) =>
        {
            _isActive = false;
            ViewModel.StopAutoRefresh();
        };
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
            PlaceholderText = L.Get("ContainersPage.MirrorPlaceholder"),
            Header = L.Get("ContainersPage.MirrorHeader"),
            Text = defaultMirror,
        };
        confirmPanel.Children.Add(mirrorBox);

        var confirm = new ContentDialog
        {
            Title = L.Get("ContainersPage.ComposeDeployTitle"),
            Content = confirmPanel,
            PrimaryButtonText = L.Get("ContainersPage.DeployButton"),
            CloseButtonText = L.Get("ContainersPage.CancelButton"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        var mirror = string.IsNullOrWhiteSpace(mirrorBox.Text) ? null : mirrorBox.Text.Trim();
        await ShowComposeProgressAsync(L.Get("ContainersPage.ComposeDeployProgressTitle"), async progress =>
        {
            var results = await ViewModel.DeployComposeAsync(path, progress, mirror);
            return string.Join(Environment.NewLine,
                results.Select(r => L.GetFormat(
                    r.Success ? "ContainersPage.DeployResultOk" : "ContainersPage.DeployResultFail",
                    r.ContainerName, r.Error)));
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
            Title = L.Get("ContainersPage.ComposeStopTitle"),
            Content = L.GetFormat("ContainersPage.ComposeStopContent",
                string.Join("\n", services.Select(s => $"• {s.ContainerName}"))),
            PrimaryButtonText = L.Get("ContainersPage.StopAndDeleteButton"),
            CloseButtonText = L.Get("ContainersPage.CancelButton"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        await ShowComposeProgressAsync(L.Get("ContainersPage.ComposeStopProgressTitle"), async progress =>
        {
            var stopped = await ViewModel.StopComposeAsync(path, progress);
            return string.Join(Environment.NewLine, stopped.Select(s => L.GetFormat("ContainersPage.RemovedItem", s)));
        });
    }

    private async Task ShowComposeProgressAsync(string title, Func<IProgress<string>, Task<string>> run)
    {
        var statusText = new TextBlock { Text = L.Get("ContainersPage.ProgressRunning") };
        var dialog = new ContentDialog
        {
            Title = title,
            Content = statusText,
            CloseButtonText = L.Get("ContainersPage.DoneButton"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        var showTask = dialog.ShowAsync();
        var progress = new Progress<string>(line => statusText.Text = line);
        try
        {
            statusText.Text = await run(progress);
        }
        catch (OperationCanceledException)
        {
            statusText.Text = L.Get("ContainersPage.Cancelled");
        }
        catch (Exception ex)
        {
            statusText.Text = L.GetFormat("ContainersPage.OperationFailed", ex.Message);
        }
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
                    Title = L.Get("ContainersPage.DeleteContainerTitle"),
                    Content = L.GetFormat("ContainersPage.DeleteContainerConfirm", item.Source.Name),
                    PrimaryButtonText = L.Get("ContainersPage.DeleteButton"),
                    CloseButtonText = L.Get("ContainersPage.CancelButton"),
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
        catch (Exception ex)
        {
            global::WSLCC_App.App.WriteLog($"Windows Terminal 启动失败：{ex}");
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe", $"/c start wslc exec -it {target} /bin/sh") { UseShellExecute = false });
            }
            catch (Exception fallbackEx)
            {
                global::WSLCC_App.App.WriteLog($"cmd 终端启动失败：{fallbackEx}");
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
            await ShowInfoDialogAsync(L.Get("ContainersPage.InspectUnavailable"), ex.Message);
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
        await ShowInfoDialogAsync(L.GetFormat("ContainersPage.ContainerDetailTitle", container.Source.Name), scroll);
    }

    private async Task ShowInfoDialogAsync(string title, object content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = L.Get("ContainersPage.CloseButton"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private async Task<ContainerCreateOptions?> ShowCreateDialogAsync()
    {
        var nameBox = new TextBox { PlaceholderText = L.Get("ContainersPage.CreateNamePlaceholder") };
        var imageBox = new TextBox { PlaceholderText = L.Get("ContainersPage.CreateImagePlaceholder") };
        var portsBox = new TextBox { PlaceholderText = L.Get("ContainersPage.CreatePortsPlaceholder") };
        var envBox = new TextBox { PlaceholderText = L.Get("ContainersPage.CreateEnvPlaceholder") };
        var commandBox = new TextBox
        {
            PlaceholderText = L.Get("ContainersPage.CreateCommandPlaceholder"),
            Header = L.Get("ContainersPage.CreateCommandHeader"),
        };
        var volumeBox = new TextBox
        {
            PlaceholderText = L.Get("ContainersPage.CreateVolumePlaceholder"),
            Header = L.Get("ContainersPage.CreateVolumeHeader"),
        };
        var autoRemove = new CheckBox { Content = L.Get("ContainersPage.CreateAutoRemove") };
        var detached = new CheckBox { Content = L.Get("ContainersPage.CreateDetached"), IsChecked = true };

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
            Title = L.Get("ContainersPage.CreateContainerTitle"),
            Content = panel,
            PrimaryButtonText = L.Get("ContainersPage.CreateButton"),
            CloseButtonText = L.Get("ContainersPage.CancelButton"),
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
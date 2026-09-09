using System.Diagnostics;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WSLCC.App.ViewModels;
using WSLCC.Core.Services;

namespace WSLCC.App.Pages;

public sealed partial class ComposePage : Page
{
    public ComposeViewModel ViewModel { get; }

    private ComposeProjectItemViewModel? _editingProject;

    public ComposePage()
    {
        InitializeComponent();
        ViewModel = new ComposeViewModel(
            WslcHost.Default.Compose, WslcHost.Default.Settings, WslcHost.Default.Audit);
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = ViewModel.LoadAsync();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var project = GetProject(sender);
        if (project is null) return;
        if (string.IsNullOrEmpty(project.FilePath)) return;
        var directory = Path.GetDirectoryName(project.FilePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true });
    }

    private async void Deploy_Click(object sender, RoutedEventArgs e)
    {
        _editingProject = null;
        var mirror = await ViewModel.GetRegistryMirrorAsync();
        ShowEditor("新建编排项目", null, null, showMirror: true, mirror);
    }

    private async void EditProject_Click(object sender, RoutedEventArgs e)
    {
        var project = GetProject(sender);
        if (project is null) return;

        var content = await ViewModel.ReadProjectFileAsync(project.FilePath);
        content ??= $"# 无法读取 compose 文件：{project.FilePath ?? "（未记录路径）"}\n# 请确认文件存在后重试，或在此处粘贴新的 compose 内容。\n";
        _editingProject = project;
        ShowEditor($"编辑项目 {project.Name}", project.FilePath ?? "（未记录路径）", content, showMirror: false, string.Empty);
    }

    private async void RebuildProject_Click(object sender, RoutedEventArgs e)
    {
        var project = GetProject(sender);
        if (project is null) return;

        var confirm = new ContentDialog
        {
            Title = "重建容器",
            Content = $"将强制删除并重新创建项目「{project.Name}」的所有容器，使用最新的 compose 配置。",
            PrimaryButtonText = "重建",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        var mirror = await ViewModel.GetRegistryMirrorAsync();
        await ShowProgressAsync("重建容器", async progress =>
        {
            var results = await ViewModel.RebuildProjectAsync(project.Name, mirror, progress);
            return string.Join(Environment.NewLine,
                results.Select(r => r.Success
                    ? $"重建成功  {r.ContainerName}"
                    : $"重建失败  {r.ContainerName}  {r.Error}"));
        });
    }

    private async void StopProject_Click(object sender, RoutedEventArgs e)
    {
        var project = GetProject(sender);
        if (project is null) return;

        var confirm = new ContentDialog
        {
            Title = "停止编排项目",
            Content = $"将停止项目「{project.Name}」的所有容器（容器保留，可随时重启）。",
            PrimaryButtonText = "停止",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        await ShowProgressAsync("停止容器", async progress =>
        {
            var stopped = await ViewModel.StopProjectAsync(project.Name, progress);
            return string.Join(Environment.NewLine, stopped.Select(s => $"已停止  {s}"));
        });
    }

    private async void RestartProject_Click(object sender, RoutedEventArgs e)
    {
        var project = GetProject(sender);
        if (project is null) return;

        await ShowProgressAsync("重启容器", async progress =>
        {
            var restarted = await ViewModel.RestartProjectAsync(project.Name, progress);
            return string.Join(Environment.NewLine, restarted.Select(s => $"已重启  {s}"));
        });
    }

    private async void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        var project = GetProject(sender);
        if (project is null) return;

        var confirm = new ContentDialog
        {
            Title = "删除编排项目",
            Content = $"将停止并删除项目「{project.Name}」的所有容器，并移除该项目的编排记录。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        await ShowProgressAsync("删除容器", async progress =>
        {
            var deleted = await ViewModel.DeleteProjectAsync(project.Name, progress);
            return string.Join(Environment.NewLine, deleted.Select(s => $"已删除  {s}"));
        });
    }

    private void ShowEditor(string title, string? filePath, string? content, bool showMirror, string defaultMirror)
    {
        EditorTitleText.Text = title;
        EditorPathText.Text = filePath ?? "未选择文件，可直接粘贴 compose 内容";
        EditorBox.Document.SetText(TextSetOptions.None, content ?? string.Empty);
        MirrorBox.Visibility = showMirror ? Visibility.Visible : Visibility.Collapsed;
        ProjectNameBox.Visibility = showMirror ? Visibility.Visible : Visibility.Collapsed;
        MirrorBox.Text = defaultMirror;
        ProjectNameBox.Text = string.Empty;
        EditorPrimaryBtn.Content = _editingProject is null ? "部署" : "保存";
        ListPanel.Visibility = Visibility.Collapsed;
        EditorPanel.Visibility = Visibility.Visible;
    }

    private void HideEditor()
    {
        _editingProject = null;
        EditorPanel.Visibility = Visibility.Collapsed;
        ListPanel.Visibility = Visibility.Visible;
    }

    private void EditorCancel_Click(object sender, RoutedEventArgs e)
        => HideEditor();

    private async void EditorLoad_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".yml");
            picker.FileTypeFilter.Add(".yaml");
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(global::WSLCC_App.App.Main!);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            var content = await File.ReadAllTextAsync(file.Path);
            EditorBox.Document.SetText(TextSetOptions.None, content);
            EditorPathText.Text = file.Path;
        }
        catch (Exception ex)
        {
            EditorPathText.Text = $"读取文件失败：{ex.Message}";
        }
    }

    private async void EditorPrimary_Click(object sender, RoutedEventArgs e)
    {
        EditorBox.Document.GetText(TextGetOptions.None, out var content);

        if (_editingProject is null)
        {
            var mirror = string.IsNullOrWhiteSpace(MirrorBox.Text) ? null : MirrorBox.Text.Trim();
            var projectName = string.IsNullOrWhiteSpace(ProjectNameBox.Text) ? null : ProjectNameBox.Text.Trim();
            var path = EditorPathText.Text;
            var hasPath = !string.IsNullOrEmpty(path) && !path.StartsWith("未选择", StringComparison.Ordinal);
            HideEditor();
            await ShowProgressAsync("Compose 部署", async progress =>
            {
                var results = await ViewModel.DeployFromContentAsync(content, hasPath ? path : null, projectName, progress, mirror);
                return string.Join(Environment.NewLine,
                    results.Select(r => r.Success
                        ? $"部署成功  {r.ContainerName}  {r.Error}"
                        : $"部署失败  {r.ContainerName}  {r.Error}"));
            });
        }
        else
        {
            var project = _editingProject;
            var targetPath = string.IsNullOrEmpty(project.FilePath)
                ? (EditorPathText.Text.StartsWith("未选择", StringComparison.Ordinal) ? null : EditorPathText.Text)
                : project.FilePath;
            var saved = await ViewModel.SaveProjectFileAsync(targetPath, content);
            HideEditor();
            await ShowInfoAsync(saved ? "已保存" : "保存失败",
                saved
                    ? $"{Path.GetFileName(targetPath ?? string.Empty)} 已更新。如需应用变更，请先停止项目再重新部署。"
                    : "无法写入 compose 文件。");
        }
    }

    private static ComposeProjectItemViewModel? GetProject(object sender)
        => sender is FrameworkElement fe
            ? fe.Tag as ComposeProjectItemViewModel ?? fe.DataContext as ComposeProjectItemViewModel
            : null;

    private async Task ShowProgressAsync(string title, Func<IProgress<string>, Task<string>> run)
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

    private async Task ShowInfoAsync(string mainText, string detail)
    {
        var dialog = new ContentDialog
        {
            Title = mainText,
            Content = detail,
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
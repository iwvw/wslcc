using System.Diagnostics;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WSLCC.App.ViewModels;
using WSLCC.Core.Services;
using WSLCC_App;

namespace WSLCC.App.Pages;

public sealed partial class ComposePage : Page
{
    public ComposeViewModel ViewModel { get; }

    private ComposeProjectItemViewModel? _editingProject;

    private bool _loaded;

    public ComposePage()
    {
        InitializeComponent();
        ViewModel = new ComposeViewModel(
            WslcHost.Default.Compose, WslcHost.Default.Settings, WslcHost.Default.Audit);
        DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (_loaded) return;
        _loaded = true;
        _ = ViewModel.LoadAsync();
    }

    private async void OpenComposeDir_Click(object sender, RoutedEventArgs e)
    {
        var dir = await ViewModel.GetComposeDirectoryAsync();
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WSLCC", "compose");
        }
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            await ShowInfoAsync(L.Get("ComposePage.NoComposeDir"));
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            global::WSLCC_App.App.WriteLog($"打开 Compose 目录失败：{ex}");
        }
    }

    private async void ChangeComposeDir_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder,
            };
            picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(
                picker, WinRT.Interop.WindowNative.GetWindowHandle(global::WSLCC_App.App.Main!));
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null) return;

            await ViewModel.SetComposeDirectoryAsync(folder.Path);
            await ViewModel.LoadAsync();
            await ShowInfoAsync(L.GetFormat("ComposePage.DirChangedFormat", folder.Path));
        }
        catch (Exception ex)
        {
            global::WSLCC_App.App.WriteLog($"更改 Compose 目录失败：{ex}");
        }
    }

    private async Task ShowInfoAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = L.Get("ComposePage.ComposeDirTitle"),
            Content = message,
            CloseButtonText = L.Get("ComposePage.OkButton"),
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var project = GetProject(sender);
        if (project is null) return;
        if (string.IsNullOrEmpty(project.FilePath)) return;
        var directory = Path.GetDirectoryName(project.FilePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            global::WSLCC_App.App.WriteLog($"打开项目目录失败：{ex}");
        }
    }

    private async void Deploy_Click(object sender, RoutedEventArgs e)
    {
        _editingProject = null;
        var mirror = await ViewModel.GetRegistryMirrorAsync();
        ShowEditor(L.Get("ComposePage.NewProjectTitle"), null, null, showMirror: true, mirror);
    }

    private async void EditProject_Click(object sender, RoutedEventArgs e)
    {
        var project = GetProject(sender);
        if (project is null) return;

        var content = await ViewModel.ReadProjectFileAsync(project.FilePath);
        content ??= L.GetFormat("ComposePage.CannotReadFile",
            project.FilePath ?? L.Get("ComposePage.NoRecordedPath"));
        _editingProject = project;
        ShowEditor(L.GetFormat("ComposePage.EditProjectTitle", project.Name),
            project.FilePath ?? L.Get("ComposePage.NoRecordedPath"), content, showMirror: false, string.Empty);
    }

    private async void RebuildProject_Click(object sender, RoutedEventArgs e)
    {
        var project = GetProject(sender);
        if (project is null) return;

        var confirm = new ContentDialog
        {
            Title = L.Get("ComposePage.RebuildTitle"),
            Content = L.GetFormat("ComposePage.RebuildConfirm", project.Name),
            PrimaryButtonText = L.Get("ComposePage.RebuildAction"),
            CloseButtonText = L.Get("ComposePage.CancelButton"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        var mirror = await ViewModel.GetRegistryMirrorAsync();
        await ShowProgressAsync(L.Get("ComposePage.RebuildProgressTitle"), async progress =>
        {
            var results = await ViewModel.RebuildProjectAsync(project.Name, mirror, progress);
            return string.Join(Environment.NewLine,
                results.Select(r => r.Success
                    ? L.GetFormat("ComposePage.RebuildResultOk", r.ContainerName)
                    : L.GetFormat("ComposePage.RebuildResultFail", r.ContainerName, r.Error)));
        });
    }

    private async void PrimaryAction_Click(object sender, RoutedEventArgs e)
    {
        var project = GetProject(sender);
        if (project is null) return;

        if (project.IsAllStopped)
        {
            await ShowProgressAsync(L.Get("ComposePage.StartProgressTitle"), async progress =>
            {
                var started = await ViewModel.StartProjectAsync(project.Name, progress);
                return string.Join(Environment.NewLine, started.Select(s => L.GetFormat("ComposePage.StartedItem", s)));
            });
            return;
        }

        var confirm = new ContentDialog
        {
            Title = L.Get("ComposePage.StopProjectTitle"),
            Content = L.GetFormat("ComposePage.StopProjectConfirm", project.Name),
            PrimaryButtonText = L.Get("ComposePage.StopButton"),
            CloseButtonText = L.Get("ComposePage.CancelButton"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        await ShowProgressAsync(L.Get("ComposePage.StopProgressTitle"), async progress =>
        {
            var stopped = await ViewModel.StopProjectAsync(project.Name, progress);
            return string.Join(Environment.NewLine, stopped.Select(s => L.GetFormat("ComposePage.StoppedItem", s)));
        });
    }

    private async void RestartProject_Click(object sender, RoutedEventArgs e)
    {
        var project = GetProject(sender);
        if (project is null) return;

        await ShowProgressAsync(L.Get("ComposePage.RestartProgressTitle"), async progress =>
        {
            var restarted = await ViewModel.RestartProjectAsync(project.Name, progress);
            return string.Join(Environment.NewLine, restarted.Select(s => L.GetFormat("ComposePage.RestartedItem", s)));
        });
    }

    private async void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        var project = GetProject(sender);
        if (project is null) return;

        var confirm = new ContentDialog
        {
            Title = L.Get("ComposePage.DeleteProjectTitle"),
            Content = L.GetFormat("ComposePage.DeleteProjectConfirm", project.Name),
            PrimaryButtonText = L.Get("ComposePage.DeleteButton"),
            CloseButtonText = L.Get("ComposePage.CancelButton"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        await ShowProgressAsync(L.Get("ComposePage.DeleteProgressTitle"), async progress =>
        {
            var deleted = await ViewModel.DeleteProjectAsync(project.Name, progress);
            return string.Join(Environment.NewLine, deleted.Select(s => L.GetFormat("ComposePage.DeletedItem", s)));
        });
    }

    private void ShowEditor(string title, string? filePath, string? content, bool showMirror, string defaultMirror)
    {
        EditorTitleText.Text = title;
        EditorPathText.Text = filePath ?? L.Get("ComposePage.NoFileSelected");
        EditorBox.Document.SetText(TextSetOptions.None, content ?? string.Empty);
        MirrorBox.Visibility = showMirror ? Visibility.Visible : Visibility.Collapsed;
        ProjectNameBox.Visibility = showMirror ? Visibility.Visible : Visibility.Collapsed;
        MirrorBox.Text = defaultMirror;
        ProjectNameBox.Text = string.Empty;
        EditorPrimaryBtn.Content = _editingProject is null
            ? L.Get("ComposePage.DeployAction")
            : L.Get("ComposePage.SaveButton");
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
            EditorPathText.Text = L.GetFormat("ComposePage.ReadFileFailed", ex.Message);
        }
    }

    private async void EditorPrimary_Click(object sender, RoutedEventArgs e)
    {
        EditorBox.Document.GetText(TextGetOptions.None, out var content);

        var noFilePlaceholder = L.Get("ComposePage.NoFileSelected");
        if (_editingProject is null)
        {
            var mirror = string.IsNullOrWhiteSpace(MirrorBox.Text) ? null : MirrorBox.Text.Trim();
            var projectName = string.IsNullOrWhiteSpace(ProjectNameBox.Text) ? null : ProjectNameBox.Text.Trim();
            var path = EditorPathText.Text;
            var hasPath = !string.IsNullOrEmpty(path)
                && !string.Equals(path, noFilePlaceholder, StringComparison.Ordinal);
            HideEditor();
            await ShowProgressAsync(L.Get("ComposePage.DeployProgressTitle"), async progress =>
            {
                var results = await ViewModel.DeployFromContentAsync(content, hasPath ? path : null, projectName, progress, mirror);
                return string.Join(Environment.NewLine,
                    results.Select(r => L.GetFormat(
                        r.Success ? "ComposePage.DeployResultOk" : "ComposePage.DeployResultFail",
                        r.ContainerName, r.Error)));
            });
        }
        else
        {
            var project = _editingProject;
            var targetPath = string.IsNullOrEmpty(project.FilePath)
                ? (string.Equals(EditorPathText.Text, noFilePlaceholder, StringComparison.Ordinal)
                    ? null
                    : EditorPathText.Text)
                : project.FilePath;
            var saved = await ViewModel.SaveProjectFileAsync(targetPath, content);
            HideEditor();
            await ShowInfoAsync(saved ? L.Get("ComposePage.SavedTitle") : L.Get("ComposePage.SaveFailedTitle"),
                saved
                    ? L.GetFormat("ComposePage.SavedDetailFormat", Path.GetFileName(targetPath ?? string.Empty))
                    : L.Get("ComposePage.CannotWriteFile"));
        }
    }

    private static ComposeProjectItemViewModel? GetProject(object sender)
        => sender is FrameworkElement fe
            ? fe.Tag as ComposeProjectItemViewModel ?? fe.DataContext as ComposeProjectItemViewModel
            : null;

    private async Task ShowProgressAsync(string title, Func<IProgress<string>, Task<string>> run)
    {
        var statusText = new TextBlock { Text = L.Get("ComposePage.ProgressRunning") };
        var dialog = new ContentDialog
        {
            Title = title,
            Content = statusText,
            CloseButtonText = L.Get("ComposePage.DoneButton"),
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
            statusText.Text = L.Get("ComposePage.Cancelled");
        }
        catch (Exception ex)
        {
            statusText.Text = L.GetFormat("ComposePage.OperationFailed", ex.Message);
        }
        await showTask;
    }

    private async Task ShowInfoAsync(string mainText, string detail)
    {
        var dialog = new ContentDialog
        {
            Title = mainText,
            Content = detail,
            CloseButtonText = L.Get("ComposePage.CloseButton"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
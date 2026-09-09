using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WSLCC.Core.Services;

namespace WSLCC.App.ViewModels;

public sealed partial class ComposeProjectItemViewModel : ObservableObject
{
    public ComposeProjectStatus Source { get; }

    public ComposeProjectItemViewModel(ComposeProjectStatus source) => Source = source;

    public string Name => Source.Name;

    public string? FilePath => Source.FilePath;

    public bool HasFilePath => !string.IsNullOrEmpty(Source.FilePath);

    public string Summary => $"{Source.RunningCount}/{Source.Services.Count} 个服务运行中";

    public bool HasMissing => Source.Services.Any(s => s.IsMissing);

    public string ServicesText => string.Join(Environment.NewLine,
        Source.Services.Select(s => $"{s.ContainerName}  [{s.State}]  {s.Status}"));
}

public partial class ComposeViewModel : ObservableObject
{
    private readonly IWslcComposeService _compose;
    private readonly IWslcSettingsService _settings;
    private readonly IWslcAuditService _audit;

    public ObservableCollection<ComposeProjectItemViewModel> Projects { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial bool HasProjects { get; set; }

    public ComposeViewModel(IWslcComposeService compose, IWslcSettingsService settings, IWslcAuditService audit)
    {
        _compose = compose;
        _settings = settings;
        _audit = audit;
        ErrorMessage = string.Empty;
    }

    public AsyncRelayCommand RefreshCommand => new(LoadAsync);

    public async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        try
        {
            var projects = await _compose.ListProjectsAsync();
            Projects.Clear();
            foreach (var project in projects)
                Projects.Add(new ComposeProjectItemViewModel(project));
            HasProjects = Projects.Count > 0;
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public Task<string> GetRegistryMirrorAsync() => _settings.GetRegistryMirrorAsync();

    public async Task<IReadOnlyList<ComposeDeploymentResult>> DeployFromContentAsync(
        string content, string? composeFilePath, string? overrideProjectName = null, IProgress<string>? progress = null, string? registryMirror = null)
    {
        try
        {
            var results = await _compose.DeployFromContentAsync(content, composeFilePath, overrideProjectName, progress, registryMirror);
            await LoadAsync();
            return results;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return Array.Empty<ComposeDeploymentResult>();
        }
    }

    public async Task<string?> ReadProjectFileAsync(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;
        try
        {
            return await File.ReadAllTextAsync(filePath);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> SaveProjectFileAsync(string? filePath, string content)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrWhiteSpace(content)) return false;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(filePath, content);
            await _audit.RecordAsync("compose", "edit", Path.GetFileName(filePath), true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> StopProjectAsync(string projectName, IProgress<string>? progress = null)
    {
        try
        {
            var stopped = await _compose.StopProjectAsync(projectName, progress);
            await LoadAsync();
            return stopped;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return Array.Empty<string>();
        }
    }

    public async Task<IReadOnlyList<string>> DeleteProjectAsync(string projectName, IProgress<string>? progress = null)
    {
        try
        {
            var deleted = await _compose.DeleteProjectAsync(projectName, progress);
            await LoadAsync();
            return deleted;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return Array.Empty<string>();
        }
    }

    public async Task<IReadOnlyList<string>> RestartProjectAsync(string projectName, IProgress<string>? progress = null)
    {
        try
        {
            var restarted = await _compose.RestartProjectAsync(projectName, progress);
            await LoadAsync();
            return restarted;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return Array.Empty<string>();
        }
    }

    public async Task<IReadOnlyList<ComposeDeploymentResult>> RebuildProjectAsync(
        string projectName, string? mirror, IProgress<string>? progress = null)
    {
        try
        {
            var results = await _compose.RebuildProjectAsync(projectName, mirror, progress);
            await LoadAsync();
            return results;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return Array.Empty<ComposeDeploymentResult>();
        }
    }

    private void ShowError(Exception ex)
    {
        ErrorMessage = ex.Message;
        HasError = true;
    }
}
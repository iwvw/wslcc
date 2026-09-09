using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using WSLCC.Core.Models;
using WSLCC.Core.Services;

namespace WSLCC.App.ViewModels;

public partial class ContainersViewModel : ObservableObject
{
    private readonly IWslcContainerService _containers;
    private readonly IWslcComposeService _compose;
    private readonly IWslcSettingsService _settings;
    private readonly DispatcherTimer _statsTimer;

    public ObservableCollection<ContainerItemViewModel> Containers { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial bool HasContainers { get; set; }

    [ObservableProperty]
    public partial ContainerItemViewModel? SelectedContainer { get; set; }

    public ContainersViewModel(
        IWslcContainerService containers, IWslcComposeService compose, IWslcSettingsService settings)
    {
        _containers = containers;
        _compose = compose;
        _settings = settings;
        ErrorMessage = string.Empty;
        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _statsTimer.Tick += async (_, _) => await RefreshStatsAsync();
    }

    public void StartAutoRefresh() => _statsTimer.Start();

    public void StopAutoRefresh() => _statsTimer.Stop();

    private async Task RefreshStatsAsync()
    {
        try
        {
            var stats = await _containers.GetStatsAsync();
            foreach (var item in Containers)
            {
                stats.TryGetValue(item.Source.Name, out var stat);
                item.UpdateStats(stat);
            }
        }
        catch
        {
        }
    }

    public AsyncRelayCommand RefreshCommand => new(LoadAsync);

    public Func<ContainerItemViewModel, Task>? InspectProvider { get; set; }

    public async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        try
        {
            var list = await _containers.ListAsync();
            var stats = await _containers.GetStatsAsync();
            Containers.Clear();
            foreach (var item in list)
            {
                stats.TryGetValue(item.Name, out var stat);
                Containers.Add(new ContainerItemViewModel(item, stat));
            }
            HasContainers = Containers.Count > 0;
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

    public Task StartContainerAsync(ContainerItemViewModel item)
        => ExecuteAsync(item, c => _containers.StartAsync(c.Source.Name));

    public Task StopContainerAsync(ContainerItemViewModel item)
        => ExecuteAsync(item, c => _containers.StopAsync(c.Source.Name));

    public Task RestartContainerAsync(ContainerItemViewModel item)
        => ExecuteAsync(item, c => _containers.RestartAsync(c.Source.Name));

    public Task DeleteContainerAsync(ContainerItemViewModel item)
        => ExecuteAsync(item, c => _containers.RemoveAsync(c.Source.Name, force: true));

    public Task<string> GetRegistryMirrorAsync() => _settings.GetRegistryMirrorAsync();

    public async Task<IReadOnlyList<ComposeDeploymentResult>> DeployComposeAsync(
        string composeFilePath, IProgress<string>? progress = null, string? registryMirror = null)
    {
        try
        {
            var results = await _compose.DeployAsync(composeFilePath, progress, registryMirror);
            await LoadAsync();
            return results;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return Array.Empty<ComposeDeploymentResult>();
        }
    }

    public async Task<IReadOnlyList<ComposeServiceDefinition>> ParseComposeAsync(string composeFilePath)
    {
        try
        {
            var info = await _compose.LoadProjectAsync(composeFilePath);
            return info.Services;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return Array.Empty<ComposeServiceDefinition>();
        }
    }

    public async Task<IReadOnlyList<string>> StopComposeAsync(
        string composeFilePath, IProgress<string>? progress = null)
    {
        try
        {
            var stopped = await _compose.StopAsync(composeFilePath, progress);
            await LoadAsync();
            return stopped;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return Array.Empty<string>();
        }
    }

    private async Task ExecuteAsync(ContainerItemViewModel item, Func<ContainerItemViewModel, Task> operation)
    {
        try
        {
            await operation(item);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ShowError(Exception ex)
    {
        ErrorMessage = ex.Message;
        HasError = true;
    }
}
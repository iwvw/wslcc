using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using WSLCC.Core.Models;
using WSLCC.Core.Services;
using WSLCC_App;

namespace WSLCC.App.ViewModels;

public partial class ContainersViewModel : ObservableObject
{
    private readonly IWslcContainerService _containers;
    private readonly IWslcComposeService _compose;
    private readonly IWslcSettingsService _settings;
    private readonly IWslcHistoryService _history;
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
    public partial string NetworkWarningText { get; set; }

    [ObservableProperty]
    public partial bool HasNetworkWarning { get; set; }

    [ObservableProperty]
    public partial ContainerItemViewModel? SelectedContainer { get; set; }

    public ContainersViewModel(
        IWslcContainerService containers, IWslcComposeService compose, IWslcSettingsService settings,
        IWslcHistoryService history)
    {
        _containers = containers;
        _compose = compose;
        _settings = settings;
        _history = history;
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
        catch (Exception ex)
        {
            global::WSLCC_App.App.WriteLog($"容器统计刷新失败：{ex}");
        }
    }

    public AsyncRelayCommand RefreshCommand => new(LoadAsync);

    public async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        CheckNetworkWarning();
        try
        {
            var list = await _containers.ListAsync();
            var stats = await _containers.GetStatsAsync();
            var snapshotPorts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var snap in await _history.QueryLatestSnapshotsAsync())
            {
                if (!string.IsNullOrEmpty(snap.Ports))
                    snapshotPorts[snap.Name] = snap.Ports;
            }
            MergeContainers(list, snapshotPorts);
            foreach (var item in Containers)
            {
                stats.TryGetValue(item.Source.Name, out var stat);
                item.UpdateStats(stat);
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

    private void MergeContainers(IReadOnlyList<ContainerItem> list, IReadOnlyDictionary<string, string> snapshotPorts)
    {
        var byName = Containers.ToDictionary(x => x.Source.Name, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in list)
        {
            seen.Add(item.Name);
            var ports = item.Ports.Length > 0
                ? item.Ports
                : snapshotPorts.TryGetValue(item.Name, out var cachedPorts) && cachedPorts.Length > 0
                    ? cachedPorts
                    : "";
            if (byName.TryGetValue(item.Name, out var vm))
            {
                var merged = ports.Length > 0 ? item with { Ports = ports } : item with { Ports = vm.Source.Ports };
                vm.UpdateSource(merged);
            }
            else
            {
                var fresh = ports.Length > 0 ? item with { Ports = ports } : item;
                Containers.Add(new ContainerItemViewModel(fresh));
            }
        }
        for (var i = Containers.Count - 1; i >= 0; i--)
        {
            if (!seen.Contains(Containers[i].Source.Name))
                Containers.RemoveAt(i);
        }
    }

    public Task StartContainerAsync(ContainerItemViewModel item)
        => ExecuteContainerOpAsync(item, c => _containers.StartAsync(c.Source.Name));

    public Task StopContainerAsync(ContainerItemViewModel item)
        => ExecuteContainerOpAsync(item, c => _containers.StopAsync(c.Source.Name));

    public Task RestartContainerAsync(ContainerItemViewModel item)
        => ExecuteContainerOpAsync(item, c => _containers.RestartAsync(c.Source.Name));

    public Task DeleteContainerAsync(ContainerItemViewModel item)
        => ExecuteRemoveAsync(item);

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

    private async Task ExecuteContainerOpAsync(
        ContainerItemViewModel item, Func<ContainerItemViewModel, Task> operation)
    {
        try
        {
            await operation(item);
            var updated = await _containers.InspectAsync(item.Source.Name);
            if (updated is not null)
            {
                var merged = updated.Ports.Length > 0
                    ? updated
                    : updated with { Ports = item.Source.Ports };
                item.UpdateSource(merged);
            }
            else
            {
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task ExecuteRemoveAsync(ContainerItemViewModel item)
    {
        try
        {
            await _containers.RemoveAsync(item.Source.Name, force: true);
            Containers.Remove(item);
            HasContainers = Containers.Count > 0;
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void CheckNetworkWarning()
    {
        var tunInterfaces = WslcNetworkDiagnostics.DetectProxyTunInterfaces();
        HasNetworkWarning = tunInterfaces.Count > 0;
        NetworkWarningText = HasNetworkWarning
            ? L.GetFormat("ContainersPage.NetworkWarning",
                string.Join(L.Get("ContainersPage.ListSeparator"), tunInterfaces))
            : "";
    }

    private void ShowError(Exception ex)
    {
        ErrorMessage = ex.Message;
        HasError = true;
    }
}
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WSLCC.App.ViewModels;
using WSLCC.Core.Models;
using WSLCC.Core.Services;
using WSLCC_App;

namespace WSLCC.App.Mini;

public sealed partial class MiniPanelViewModel : ObservableObject
{
    private readonly IWslcContainerService _containers;

    private bool _isBusy;
    private bool _isLoading;

    public ObservableCollection<ContainerItemViewModel> Containers { get; } = new();

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial bool HasContainers { get; set; }

    [ObservableProperty]
    public partial string SummaryText { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    public MiniPanelViewModel(IWslcContainerService containers)
    {
        _containers = containers;
        StatusText = L.Get("MiniPanel.Ready");
        SummaryText = string.Empty;
    }

    public async Task LoadAsync()
    {
        if (_isLoading) return;
        _isLoading = true;
        IsRefreshing = true;
        try
        {
            var list = await _containers.ListAsync();
            IReadOnlyDictionary<string, ContainerStats> stats;
            try
            {
                stats = await _containers.GetStatsAsync();
            }
            catch (Exception ex)
            {
                stats = new Dictionary<string, ContainerStats>();
                global::WSLCC_App.App.WriteLog($"迷你面板统计获取失败：{ex}");
            }

            Merge(list);
            foreach (var item in Containers)
            {
                stats.TryGetValue(item.Source.Name, out var stat);
                item.UpdateStats(stat);
            }

            HasContainers = Containers.Count > 0;
            var running = Containers.Count(c => c.IsRunning);
            SummaryText = L.GetFormat("MiniPanel.Summary", running, Containers.Count - running);
            StatusText = L.Get("MiniPanel.Ready");
        }
        catch (Exception ex)
        {
            StatusText = L.GetFormat("MiniPanel.Failed", ex.Message);
        }
        finally
        {
            _isLoading = false;
            IsRefreshing = false;
        }
    }

    public async Task RefreshStatsAsync()
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
            global::WSLCC_App.App.WriteLog($"迷你面板统计刷新失败：{ex}");
        }
    }

    private void Merge(IReadOnlyList<ContainerItem> list)
    {
        var byName = Containers.ToDictionary(x => x.Source.Name, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in list)
        {
            seen.Add(item.Name);
            if (byName.TryGetValue(item.Name, out var vm))
                vm.UpdateSource(item);
            else
                Containers.Add(new ContainerItemViewModel(item));
        }
        for (var i = Containers.Count - 1; i >= 0; i--)
        {
            if (!seen.Contains(Containers[i].Source.Name))
                Containers.RemoveAt(i);
        }
    }

    public Task StartAsync(ContainerItemViewModel item) => RunAsync(item, "start", _containers.StartAsync);
    public Task StopAsync(ContainerItemViewModel item) => RunAsync(item, "stop", _containers.StopAsync);
    public Task RestartAsync(ContainerItemViewModel item) => RunAsync(item, "restart", _containers.RestartAsync);

    private async Task RunAsync(
        ContainerItemViewModel item, string action, Func<string, CancellationToken, Task> operation)
    {
        if (_isBusy) return;
        _isBusy = true;
        StatusText = L.GetFormat("MiniPanel.Working", item.Name);
        try
        {
            await operation(item.Source.Name, default);
            await LoadAsync();
            StatusText = L.GetFormat($"MiniPanel.Done.{action}", item.Name);
        }
        catch (Exception ex)
        {
            StatusText = L.GetFormat("MiniPanel.Failed", ex.Message);
        }
        finally
        {
            _isBusy = false;
        }
    }
}

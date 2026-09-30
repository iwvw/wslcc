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
    private readonly WslcHost _host;
    private readonly WSLCC.App.Services.ContainerIconService _icons = WSLCC.App.Services.ContainerIconService.Shared;

    private bool _isBusy;
    private bool _isLoading;
    private bool _sessionBusy;

    public ObservableCollection<ContainerItemViewModel> Containers { get; } = new();

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial bool HasStatus { get; set; }

    [ObservableProperty]
    public partial bool HasContainers { get; set; }

    [ObservableProperty]
    public partial string SummaryText { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial bool IsSessionRunning { get; set; }

    [ObservableProperty]
    public partial bool IsSessionBusy { get; set; }

    public MiniPanelViewModel(WslcHost host)
    {
        _host = host;
        _containers = host.Containers;
        StatusText = string.Empty;
        SummaryText = string.Empty;
    }

    public async Task LoadAsync()
    {
        if (_isLoading) return;
        _isLoading = true;
        IsRefreshing = true;
        try
        {
            IsSessionRunning = await DetectSessionAsync();
            if (!IsSessionRunning)
            {
                // 会话未运行时不查询容器，否则会隐式把会话重新拉起。
                ClearContainers();
                SummaryText = string.Empty;
                HasStatus = false;
                StatusText = string.Empty;
                return;
            }

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
            StatusText = string.Empty;
            HasStatus = false;
            _ = ResolveIconsAsync();
        }
        catch (Exception ex)
        {
            StatusText = L.GetFormat("MiniPanel.Failed", ex.Message);
            HasStatus = true;
        }
        finally
        {
            _isLoading = false;
            IsRefreshing = false;
        }
    }

    public async Task RefreshStatsAsync()
    {
        // 会话未运行时不做容器查询，否则会隐式把会话重新拉起。
        if (!IsSessionRunning) return;
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

    private async Task<bool> DetectSessionAsync()
    {
        try
        {
            var sessions = await _host.System.ListSessionsAsync();
            return sessions.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public Task ToggleSessionAsync() => IsSessionRunning ? StopSessionAsync() : StartSessionAsync();

    private async Task StartSessionAsync()
    {
        if (_sessionBusy) return;
        _sessionBusy = true;
        IsSessionBusy = true;
        StatusText = L.Get("MiniPanel.SessionStarting");
        HasStatus = true;
        try
        {
            // 会话由容器命令隐式拉起：跑一次容器列表即会创建会话。
            await _containers.ListAsync();
            var ready = await WaitForSessionAsync();
            IsSessionRunning = ready;
            StatusText = ready ? L.Get("MiniPanel.SessionStarted") : L.Get("MiniPanel.SessionStartSlow");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText = L.GetFormat("MiniPanel.Failed", ex.Message);
        }
        finally
        {
            _sessionBusy = false;
            IsSessionBusy = false;
        }
    }

    private async Task StopSessionAsync()
    {
        if (_sessionBusy) return;
        _sessionBusy = true;
        IsSessionBusy = true;
        StatusText = L.Get("MiniPanel.SessionStopping");
        HasStatus = true;
        try
        {
            await _host.System.TerminateSessionsAsync();
            // 终止后立即判定为已停止：不再调用容器列表，否则会隐式把会话重新拉起。
            IsSessionRunning = false;
            ClearContainers();
            SummaryText = string.Empty;
            StatusText = L.Get("MiniPanel.SessionStopped");
            HasStatus = true;
        }
        catch (Exception ex)
        {
            StatusText = L.GetFormat("MiniPanel.Failed", ex.Message);
        }
        finally
        {
            _sessionBusy = false;
            IsSessionBusy = false;
        }
    }

    private void ClearContainers()
    {
        Containers.Clear();
        HasContainers = false;
    }

    private async Task<bool> WaitForSessionAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (await DetectSessionAsync()) return true;
            await Task.Delay(1500);
        }
        return false;
    }

    private void Merge(IReadOnlyList<ContainerItem> list)
    {        var byName = Containers.ToDictionary(x => x.Source.Name, StringComparer.OrdinalIgnoreCase);
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

    private async Task ResolveIconsAsync()
    {
        foreach (var item in Containers.ToList())
        {
            if (!item.ShouldResolveIcon()) continue;
            var url = item.WebUrl;
            if (url is null) continue;
            try
            {
                var cached = _icons.TryGetCached(url);
                var path = cached ?? await _icons.ResolveAsync(url);
                if (path is null)
                {
                    item.ApplyIcon(null);
                    continue;
                }
                var source = await WSLCC.App.Services.ContainerIconLoader.LoadAsync(path);
                item.ApplyIcon(source);
            }
            catch
            {
                item.ApplyIcon(null);
            }
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
        HasStatus = true;
        try
        {
            await operation(item.Source.Name, default);
            await LoadAsync();
            StatusText = L.GetFormat($"MiniPanel.Done.{action}", item.Name);
            HasStatus = true;
        }
        catch (Exception ex)
        {
            StatusText = L.GetFormat("MiniPanel.Failed", ex.Message);
            HasStatus = true;
        }
        finally
        {
            _isBusy = false;
        }
    }
}

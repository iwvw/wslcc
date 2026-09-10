using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WSLCC.Core.Models;
using WSLCC.Core.Services;
using WSLCC_App;

namespace WSLCC.App.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly WslcHost _host;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string WslcVersion { get; set; }

    [ObservableProperty]
    public partial string KernelVersion { get; set; }

    [ObservableProperty]
    public partial string WindowsVersion { get; set; }

    [ObservableProperty]
    public partial string SettingsFile { get; set; }

    [ObservableProperty]
    public partial string SessionManagerVersion { get; set; }

    [ObservableProperty]
    public partial string ActiveSessions { get; set; }

    [ObservableProperty]
    public partial string MissingComponents { get; set; }

    [ObservableProperty]
    public partial string ApiStatus { get; set; }

    [ObservableProperty]
    public partial string ImagesCount { get; set; }

    [ObservableProperty]
    public partial string ContainersCount { get; set; }

    [ObservableProperty]
    public partial string RunningContainersCount { get; set; }

    [ObservableProperty]
    public partial string StoppedContainersCount { get; set; }

    [ObservableProperty]
    public partial string QuotaCpu { get; set; }

    [ObservableProperty]
    public partial string QuotaMemory { get; set; }

    [ObservableProperty]
    public partial string QuotaStorage { get; set; }

    [ObservableProperty]
    public partial string TodayText { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool HasStatus { get; set; }

    [ObservableProperty]
    public partial string NetworkWarningText { get; set; }

    [ObservableProperty]
    public partial bool HasNetworkWarning { get; set; }

    [ObservableProperty]
    public partial string DatabasePath { get; set; }

    [ObservableProperty]
    public partial string AuditCount { get; set; }

    [ObservableProperty]
    public partial string PullCount { get; set; }

    [ObservableProperty]
    public partial string SnapshotCount { get; set; }

    public ObservableCollection<string> SessionList { get; } = new();

    [ObservableProperty]
    public partial bool HasGuidance { get; set; }

    public ObservableCollection<string> GuidanceSteps { get; } = new();

    public string ContainerDetailText
        => L.GetFormat("Home.ContainerDetail", RunningContainersCount, StoppedContainersCount);

    partial void OnRunningContainersCountChanged(string value)
        => OnPropertyChanged(nameof(ContainerDetailText));

    partial void OnStoppedContainersCountChanged(string value)
        => OnPropertyChanged(nameof(ContainerDetailText));

    public HomeViewModel(WslcHost host)
    {
        _host = host;
        WslcVersion = "-";
        KernelVersion = "-";
        WindowsVersion = "-";
        SettingsFile = "-";
        SessionManagerVersion = "-";
        ActiveSessions = "-";
        MissingComponents = L.Get("Home.Unknown");
        ApiStatus = "-";
        ImagesCount = "-";
        ContainersCount = "-";
        RunningContainersCount = "-";
        StoppedContainersCount = "-";
        QuotaCpu = "-";
        QuotaMemory = "-";
        QuotaStorage = "-";
        TodayText = L.GetFormat("Home.TodayText", DateTime.Today);
        DatabasePath = "-";
        AuditCount = "-";
        PullCount = "-";
        SnapshotCount = "-";
    }

    public AsyncRelayCommand RefreshCommand => new(LoadAsync);

    public async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        HasGuidance = false;
        HasStatus = false;
        StatusMessage = "";
        var tunInterfaces = WslcNetworkDiagnostics.DetectProxyTunInterfaces();
        HasNetworkWarning = tunInterfaces.Count > 0;
        NetworkWarningText = HasNetworkWarning
            ? L.GetFormat("Home.NetworkWarning", string.Join(L.Get("Home.NetworkInterfaceSeparator"), tunInterfaces))
            : "";
        try
        {
            var info = await _host.Environment.GetEnvironmentAsync();
            if (info.Client is null && info.Server is null)
            {
                ErrorMessage = L.Get("Home.EnvUnavailable");
                HasError = true;
                BuildGuidance(ErrorMessage);
            }
            if (info.Client is { } client)
            {
                WslcVersion = client.Version;
                KernelVersion = client.KernelVersion;
                WindowsVersion = client.WindowsVersion;
                SettingsFile = client.SettingsFile;
            }
            if (info.Server is { } server)
            {
                SessionManagerVersion = server.SessionManagerVersion;
                ActiveSessions = server.Sessions.Count.ToString();
                SessionList.Clear();
                foreach (var s in server.Sessions)
                    SessionList.Add($"{s.Name} (ID {s.Id})");
            }

            var componentsOk = info.MissingComponents.Count == 0;
            MissingComponents = componentsOk ? L.Get("Home.ComponentsReady") : string.Join(", ", info.MissingComponents);
            ApiStatus = info.ApiAvailable ? L.Get("Home.ApiStatusNative") : L.Get("Home.ApiStatusCli");

            DatabasePath = _host.Database.DatabasePath;
            AuditCount = (await _host.Audit.CountAsync()).ToString();
            PullCount = (await _host.History.QueryPullsAsync()).Count.ToString();
            SnapshotCount = (await _host.History.QueryLatestSnapshotsAsync()).Count.ToString();
            var quota = await _host.System.GetResourceQuotaAsync();
            QuotaCpu = quota.Cpu;
            QuotaMemory = quota.Memory;
            QuotaStorage = quota.Storage;

            await LoadCountsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
            BuildGuidance(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildGuidance(string message)
    {
        GuidanceSteps.Clear();
        var text = message ?? "";
        var missingWslc = text.Contains("无法识别的命令", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not recognized", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not found", StringComparison.OrdinalIgnoreCase);
        var sessionIssue = text.Contains("会话", StringComparison.OrdinalIgnoreCase)
            || text.Contains("session", StringComparison.OrdinalIgnoreCase)
            || text.Contains("VM", StringComparison.OrdinalIgnoreCase)
            || text.Contains("TUN", StringComparison.OrdinalIgnoreCase)
            || text.Contains("虚拟网卡", StringComparison.OrdinalIgnoreCase);
        var networkIssue = text.Contains("网络", StringComparison.OrdinalIgnoreCase)
            || text.Contains("超时", StringComparison.OrdinalIgnoreCase)
            || text.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || text.Contains("拒绝", StringComparison.OrdinalIgnoreCase)
            || text.Contains("refused", StringComparison.OrdinalIgnoreCase);

        GuidanceSteps.Add(missingWslc
            ? L.Get("Home.GuidanceMissingWslc")
            : L.Get("Home.GuidanceConfirmWslc"));

        GuidanceSteps.Add(L.Get("Home.GuidanceStartSession"));

        GuidanceSteps.Add(sessionIssue
            ? L.Get("Home.GuidanceTunMode")
            : L.Get("Home.GuidanceTunFallback"));

        if (networkIssue)
            GuidanceSteps.Add(L.Get("Home.GuidanceNetwork"));

        GuidanceSteps.Add(L.Get("Home.GuidanceRetry"));

        HasGuidance = true;
    }

    public async Task TerminateSessionsAsync()
    {
        try
        {
            await _host.System.TerminateSessionsAsync();
            var ready = await WaitForSessionReadyAsync();
            await LoadAsync();
            StatusMessage = ready
                ? L.Get("Home.TerminateReady")
                : L.Get("Home.TerminateSlow");
            HasStatus = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
        }
    }

    public async Task RestartSessionsAsync()
    {
        try
        {
            await _host.System.TerminateSessionsAsync();
            var ready = await WaitForSessionReadyAsync();
            await LoadAsync();
            StatusMessage = ready
                ? L.Get("Home.RestartReady")
                : L.Get("Home.RestartRecovering");
            HasStatus = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
        }
    }

    private async Task<bool> WaitForSessionReadyAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var env = await _host.Environment.GetEnvironmentAsync();
                if (env.Server is { Sessions.Count: > 0 })
                    return true;
            }
            catch
            {
            }
            await Task.Delay(2000);
        }
        return false;
    }

    private async Task LoadCountsAsync()
    {
        try
        {
            ImagesCount = (await _host.Images.ListAsync()).Count.ToString();
        }
        catch
        {
            ImagesCount = "-";
        }
        try
        {
            var containers = await _host.Containers.ListAsync();
            ContainersCount = containers.Count.ToString();
            RunningContainersCount = containers.Count(c => c.State.Equals("running", StringComparison.OrdinalIgnoreCase)).ToString();
            StoppedContainersCount = containers.Count(c => !c.State.Equals("running", StringComparison.OrdinalIgnoreCase)).ToString();
        }
        catch
        {
            ContainersCount = "-";
            RunningContainersCount = "-";
            StoppedContainersCount = "-";
        }
    }
}
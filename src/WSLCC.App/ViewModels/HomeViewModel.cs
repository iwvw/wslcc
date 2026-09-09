using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WSLCC.Core.Models;
using WSLCC.Core.Services;

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
    public partial string HealthText { get; set; }

    [ObservableProperty]
    public partial bool IsHealthy { get; set; }

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
    public partial string DatabasePath { get; set; }

    [ObservableProperty]
    public partial string AuditCount { get; set; }

    [ObservableProperty]
    public partial string PullCount { get; set; }

    [ObservableProperty]
    public partial string SnapshotCount { get; set; }

    public ObservableCollection<string> SessionList { get; } = new();

    public string ContainerDetailText
        => $"{RunningContainersCount} 运行 · {StoppedContainersCount} 停止";

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
        MissingComponents = "未知";
        ApiStatus = "-";
        HealthText = "检测中";
        IsHealthy = false;
        ImagesCount = "-";
        ContainersCount = "-";
        RunningContainersCount = "-";
        StoppedContainersCount = "-";
        QuotaCpu = "-";
        QuotaMemory = "-";
        QuotaStorage = "-";
        TodayText = DateTime.Today.ToString("yyyy年M月d日 dddd", System.Globalization.CultureInfo.GetCultureInfo("zh-CN"));
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
        try
        {
            var info = await _host.Environment.GetEnvironmentAsync();
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
            MissingComponents = componentsOk ? "组件齐全" : string.Join(", ", info.MissingComponents);
            ApiStatus = info.ApiAvailable ? "可用（原生 API）" : "不可用（当前使用 CLI 通道）";
            IsHealthy = componentsOk;
            HealthText = componentsOk ? "运行正常" : "需要关注：缺失组件";

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
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task TerminateSessionsAsync()
    {
        try
        {
            await _host.System.TerminateSessionsAsync();
            await LoadAsync();
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
            await Task.Delay(TimeSpan.FromSeconds(3));
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
        }
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
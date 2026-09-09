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

    [ObservableProperty]
    public partial bool HasGuidance { get; set; }

    public ObservableCollection<string> GuidanceSteps { get; } = new();

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
        HasGuidance = false;
        try
        {
            var info = await _host.Environment.GetEnvironmentAsync();
            if (info.Client is null && info.Server is null)
            {
                ErrorMessage = "未获取到 wslc 环境信息，可能会话未启动或 wslc 未安装。";
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
            MissingComponents = componentsOk ? "组件齐全" : string.Join(", ", info.MissingComponents);
            ApiStatus = info.ApiAvailable ? "可用（原生 API）" : "不可用（当前使用 CLI 通道）";

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
            ? "未检测到 wslc 命令：请先安装微软 WSL 容器 CLI（WSL 容器文档中可查看安装方式）"
            : "确认 wslc 已安装并在 PATH 中，命令行执行 wslc --version 验证");

        GuidanceSteps.Add("启动会话：执行 wslc session start；若之前执行过 wsl --shutdown，需要重新启动 WSL");

        GuidanceSteps.Add(sessionIssue
            ? "会话反复启动失败时，检查是否开启了代理软件的 TUN 模式（如 mihomo/Clash），关闭 TUN 后重试"
            : "若反复失败，检查是否有代理软件占用虚拟网卡（TUN 模式），关闭后重试");

        if (networkIssue)
            GuidanceSteps.Add("网络异常时检查镜像加速配置：设置页面确认使用可用镜像源（如 docker.1panel.live）");

        GuidanceSteps.Add("完成上述检查后，点击右上角“刷新”重试，或重启应用");

        HasGuidance = true;
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
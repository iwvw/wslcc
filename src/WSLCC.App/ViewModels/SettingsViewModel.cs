using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WSLCC.Core.Services;
using WSLCC_App;

namespace WSLCC.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly WslcHost _host;

    [ObservableProperty]
    public partial string SessionName { get; set; }

    [ObservableProperty]
    public partial string StoragePath { get; set; }

    [ObservableProperty]
    public partial string CpuCount { get; set; }

    [ObservableProperty]
    public partial string MemoryMb { get; set; }

    [ObservableProperty]
    public partial string RegistryMirror { get; set; }

    [ObservableProperty]
    public partial bool MicaEnabled { get; set; }

    [ObservableProperty]
    public partial string ComposeDirectory { get; set; }

    public bool IsLoaded { get; set; }

    [ObservableProperty]
    public partial string DatabasePath { get; set; }

    [ObservableProperty]
    public partial string AuditCount { get; set; }

    [ObservableProperty]
    public partial string PullCount { get; set; }

    [ObservableProperty]
    public partial string SnapshotCount { get; set; }

    [ObservableProperty]
    public partial string SaveMessage { get; set; }

    [ObservableProperty]
    public partial bool HasSaveMessage { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string WslcCurrentVersion { get; set; }

    [ObservableProperty]
    public partial string WslcLatestVersion { get; set; }

    [ObservableProperty]
    public partial string WslcActionText { get; set; }

    [ObservableProperty]
    public partial string WslcActionMessage { get; set; }

    public record CloseBehaviorOption(string Value, string Label);

    [ObservableProperty]
    public partial string CloseBehavior { get; set; }


    [ObservableProperty]
    public partial bool StartupEnabled { get; set; }

    [ObservableProperty]
    public partial bool StartupContainersEnabled { get; set; }

    [ObservableProperty]
    public partial bool AutoCheckUpdate { get; set; }

    [ObservableProperty]
    public partial bool MinimizeTrayNotify { get; set; }

    public ObservableCollection<CloseBehaviorOption> CloseBehaviorOptions { get; } = new()
    {
        new("ask", L.Get("Settings.CloseBehaviorAsk")),
        new("tray", L.Get("Settings.CloseBehaviorTray")),
        new("exit", L.Get("Settings.CloseBehaviorExit")),
    };

    public bool HasWslcAction => true;

    public SettingsViewModel(WslcHost host)
    {
        _host = host;
        SessionName = string.Empty;
        StoragePath = string.Empty;
        CpuCount = string.Empty;
        MemoryMb = string.Empty;
        RegistryMirror = string.Empty;
        ComposeDirectory = string.Empty;
        DatabasePath = "-";
        AuditCount = "-";
        PullCount = "-";
        SnapshotCount = "-";
        SaveMessage = string.Empty;
        ErrorMessage = string.Empty;
        WslcCurrentVersion = "-";
        WslcLatestVersion = "-";
        WslcActionText = L.Get("Settings.CheckWslcButtonAction");
        WslcActionMessage = string.Empty;
        CloseBehavior = "ask";
    }

    public AsyncRelayCommand LoadCommand => new(LoadAsync);

    public AsyncRelayCommand SaveCommand => new(SaveAsync);

    public AsyncRelayCommand CheckWslcCommand => new(CheckWslcAsync);

    public AsyncRelayCommand InstallWslcCommand => new(InstallWslcAsync);

    private async Task CheckWslcAsync()
    {
        WslcActionMessage = L.Get("Settings.Checking");
        var info = await _host.Install.GetStatusAsync();
        WslcCurrentVersion = info.WslcInstalled ? info.CurrentWslcVersion! : L.Get("Settings.NotDetected");
        WslcLatestVersion = info.LatestWslVersion ?? L.Get("Settings.QueryFailed");
        if (info.HasUpdate)
        {
            WslcActionText = L.Get("Settings.WslcActionUpgrade");
            WslcActionMessage = L.Get("Settings.UpdateAvailable");
        }
        else if (!info.WslcInstalled)
        {
            WslcActionText = L.Get("Settings.WslcActionInstall");
            WslcActionMessage = L.Get("Settings.WslcNotInstalled");
        }
        else
        {
            WslcActionText = L.Get("Settings.WslcActionRecheck");
            WslcActionMessage = info.Error is not null ? L.GetFormat("Settings.CheckFailed", info.Error) : L.Get("Settings.UpToDate");
        }
    }

    private async Task InstallWslcAsync()
        => WslcActionMessage = await _host.Install.RunInstallOrUpgradeAsync();

    public async Task LoadAsync()
    {
        HasError = false;
        try
        {
            var config = await _host.Settings.GetSessionConfigAsync();
            SessionName = config.Name;
            StoragePath = config.StoragePath;
            CpuCount = config.CpuCount;
            MemoryMb = config.MemoryMb;
            RegistryMirror = await _host.Settings.GetRegistryMirrorAsync();
            MicaEnabled = await _host.Settings.GetMicaEnabledAsync();
            ComposeDirectory = await _host.Settings.GetComposeDirectoryAsync();

            DatabasePath = _host.Database.DatabasePath;
            AuditCount = (await _host.Audit.CountAsync()).ToString();
            PullCount = (await _host.History.QueryPullsAsync()).Count.ToString();
            SnapshotCount = (await _host.History.QueryLatestSnapshotsAsync()).Count.ToString();
            CloseBehavior = await _host.Settings.GetCloseBehaviorAsync();
            StartupEnabled = _host.Startup.IsEnabled();
            StartupContainersEnabled = await _host.Settings.GetStartupContainersEnabledAsync();
            AutoCheckUpdate = await _host.Settings.GetAutoCheckUpdateEnabledAsync();
            MinimizeTrayNotify = await _host.Settings.GetMinimizeNotifyEnabledAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
        }
        finally
        {
            IsLoaded = true;
        }
    }

    public async Task ApplyMicaAsync(bool enabled)
    {
        if (!IsLoaded) return;
        try
        {
            MicaEnabled = enabled;
            await _host.Settings.SetMicaEnabledAsync(enabled);
            global::WSLCC_App.App.Main?.ApplyBackdrop(enabled);
            SaveMessage = L.Get("Settings.MicaApplied");
            HasSaveMessage = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
        }
    }

    public async Task SaveAsync()
    {
        HasSaveMessage = false;
        HasError = false;
        try
        {
            await _host.Settings.SetSessionConfigAsync(new SessionConfig(
                string.IsNullOrWhiteSpace(SessionName) ? WslcHost.SessionName : SessionName.Trim(),
                string.IsNullOrWhiteSpace(StoragePath) ? WslcHost.DefaultStoragePath : StoragePath.Trim(),
                CpuCount.Trim(),
                MemoryMb.Trim()));
            await _host.Settings.SetRegistryMirrorAsync(RegistryMirror);
            await _host.Settings.SetComposeDirectoryAsync(ComposeDirectory);
            await _host.Settings.SetCloseBehaviorAsync(CloseBehavior);
            _host.Startup.SetEnabled(StartupEnabled);
            await _host.Settings.SetStartupContainersEnabledAsync(StartupContainersEnabled);
await _host.Settings.SetAutoCheckUpdateEnabledAsync(AutoCheckUpdate);
            await _host.Settings.SetMinimizeNotifyEnabledAsync(MinimizeTrayNotify);
            SaveMessage = L.Get("Settings.Saved");
            HasSaveMessage = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
        }
    }
}
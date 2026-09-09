using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WSLCC.Core.Services;

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
    }

    public AsyncRelayCommand LoadCommand => new(LoadAsync);

    public AsyncRelayCommand SaveCommand => new(SaveAsync);

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
            SaveMessage = "背景效果已应用";
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
            SaveMessage = "设置已保存";
            HasSaveMessage = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
        }
    }
}
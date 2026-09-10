using WSLCC.Data;

namespace WSLCC.Core.Services;

public sealed record SessionConfig(string Name, string StoragePath, string CpuCount, string MemoryMb);

public interface IWslcSettingsService
{
    Task<SessionConfig> GetSessionConfigAsync();
    Task SetSessionConfigAsync(SessionConfig config);
    Task<string> GetRegistryMirrorAsync();
    Task SetRegistryMirrorAsync(string mirror);
    Task<bool> GetMicaEnabledAsync();
    Task SetMicaEnabledAsync(bool enabled);
    Task<string> GetThemeAsync();
    Task SetThemeAsync(string theme);
    Task<string> GetComposeDirectoryAsync();
    Task SetComposeDirectoryAsync(string directory);
    Task<string> GetCloseBehaviorAsync();
    Task SetCloseBehaviorAsync(string behavior);
    Task<bool> GetStartupContainersEnabledAsync();
    Task SetStartupContainersEnabledAsync(bool enabled);
    Task<bool> GetAutoCheckUpdateEnabledAsync();
    Task SetAutoCheckUpdateEnabledAsync(bool enabled);
    Task<bool> GetMinimizeNotifyEnabledAsync();
    Task SetMinimizeNotifyEnabledAsync(bool enabled);
    Task<string> GetLanguageAsync();
    Task SetLanguageAsync(string language);
    Task<Dictionary<string, string>> GetAllAsync();
}

public sealed class WslcSettingsService : IWslcSettingsService
{
    private const string KeyName = "session.name";
    private const string KeyPath = "session.storagePath";
    private const string KeyCpu = "session.cpuCount";
    private const string KeyMem = "session.memoryMb";
    private const string KeyMirror = "registry.mirror";
    private const string KeyMica = "ui.mica";
    private const string KeyTheme = "ui.theme";
    private const string KeyComposeDir = "compose.defaultDir";
    private const string KeyCloseBehavior = "app.closeBehavior";
    private const string KeyStartupContainers = "app.startupContainers";
    private const string KeyAutoCheckUpdate = "app.autoCheckUpdate";
    private const string KeyMinimizeNotify = "app.minimizeTrayNotify";
    private const string KeyLanguage = "app.language";

    public const string DefaultRegistryMirror = "docker.1panel.live";

    private readonly SettingsRepository _repository;

    public WslcSettingsService(SettingsRepository repository) => _repository = repository;

    public async Task<SessionConfig> GetSessionConfigAsync()
    {
        var all = await _repository.GetAllAsync();
        return new SessionConfig(
            all.TryGetValue(KeyName, out var n) ? n : WslcHost.SessionName,
            all.TryGetValue(KeyPath, out var p) ? p : WslcHost.DefaultStoragePath,
            all.TryGetValue(KeyCpu, out var c) ? c : string.Empty,
            all.TryGetValue(KeyMem, out var m) ? m : string.Empty);
    }

    public async Task SetSessionConfigAsync(SessionConfig config)
    {
        await _repository.SetAsync(KeyName, config.Name);
        await _repository.SetAsync(KeyPath, config.StoragePath);
        await _repository.SetAsync(KeyCpu, config.CpuCount);
        await _repository.SetAsync(KeyMem, config.MemoryMb);
    }

    public async Task<string> GetRegistryMirrorAsync()
        => await _repository.GetAsync(KeyMirror) ?? DefaultRegistryMirror;

    public Task SetRegistryMirrorAsync(string mirror)
        => _repository.SetAsync(KeyMirror, string.IsNullOrWhiteSpace(mirror) ? DefaultRegistryMirror : mirror.Trim());

    public async Task<bool> GetMicaEnabledAsync()
        => (await _repository.GetAsync(KeyMica)) != "0";

    public Task SetMicaEnabledAsync(bool enabled)
        => _repository.SetAsync(KeyMica, enabled ? "1" : "0");

    public async Task<string> GetThemeAsync()
        => await _repository.GetAsync(KeyTheme) ?? "default";

    public Task SetThemeAsync(string theme)
        => _repository.SetAsync(KeyTheme, string.IsNullOrWhiteSpace(theme) ? "default" : theme.Trim());

    public async Task<string> GetComposeDirectoryAsync()
        => await _repository.GetAsync(KeyComposeDir) ?? string.Empty;

    public Task SetComposeDirectoryAsync(string directory)
        => _repository.SetAsync(KeyComposeDir, directory.Trim());

    public async Task<string> GetCloseBehaviorAsync()
        => await _repository.GetAsync(KeyCloseBehavior) ?? "ask";

    public Task SetCloseBehaviorAsync(string behavior)
        => _repository.SetAsync(KeyCloseBehavior, behavior is "tray" or "exit" ? behavior : "ask");

    public async Task<bool> GetStartupContainersEnabledAsync()
        => (await _repository.GetAsync(KeyStartupContainers)) == "1";

    public Task SetStartupContainersEnabledAsync(bool enabled)
        => _repository.SetAsync(KeyStartupContainers, enabled ? "1" : "0");

    public async Task<bool> GetAutoCheckUpdateEnabledAsync()
        => (await _repository.GetAsync(KeyAutoCheckUpdate)) != "0";

    public Task SetAutoCheckUpdateEnabledAsync(bool enabled)
        => _repository.SetAsync(KeyAutoCheckUpdate, enabled ? "1" : "0");

    public async Task<bool> GetMinimizeNotifyEnabledAsync()
        => (await _repository.GetAsync(KeyMinimizeNotify)) != "0";

    public Task SetMinimizeNotifyEnabledAsync(bool enabled)
        => _repository.SetAsync(KeyMinimizeNotify, enabled ? "1" : "0");

    public async Task<string> GetLanguageAsync()
        => await _repository.GetAsync(KeyLanguage) ?? "system";

    public Task SetLanguageAsync(string language)
        => _repository.SetAsync(KeyLanguage, language is "zh-Hans" or "en-US" ? language : "system");

    public Task<Dictionary<string, string>> GetAllAsync() => _repository.GetAllAsync();
}
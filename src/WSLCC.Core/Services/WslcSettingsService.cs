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

    public Task SetSessionConfigAsync(SessionConfig config)
        => Task.WhenAll(
            _repository.SetAsync(KeyName, config.Name),
            _repository.SetAsync(KeyPath, config.StoragePath),
            _repository.SetAsync(KeyCpu, config.CpuCount),
            _repository.SetAsync(KeyMem, config.MemoryMb));

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

    public Task<Dictionary<string, string>> GetAllAsync() => _repository.GetAllAsync();
}
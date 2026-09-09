using System.Text.Json;
using WSLCC.Core.Cli;
using WSLCC.Core.Models;

namespace WSLCC.Core.Services;

public interface IWslcVolumeService
{
    Task<IReadOnlyList<VolumeItem>> ListAsync(CancellationToken ct = default);
    Task DeleteAsync(string name, CancellationToken ct = default);
}

public sealed class WslcVolumeService : IWslcVolumeService
{
    private readonly WslcRunner _runner;
    private readonly IWslcAuditService _audit;

    public WslcVolumeService(WslcRunner runner, IWslcAuditService audit)
    {
        _runner = runner;
        _audit = audit;
    }

    public async Task<IReadOnlyList<VolumeItem>> ListAsync(CancellationToken ct = default)
    {
        var lines = await _runner.RunJsonLinesAsync(["volume", "list", "--format", "json"], ct).ConfigureAwait(false);
        return lines.Select(Parse).ToList();
    }

    public async Task DeleteAsync(string name, CancellationToken ct = default)
    {
        var started = DateTimeOffset.Now;
        try
        {
            await _runner.RunAsync(["volume", "rm", name], ct: ct).ConfigureAwait(false);
            await SafeRecordAsync(() => _audit.RecordAsync("volume", "delete", name, true, durationMs: ElapsedMs(started))).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
            {
                await SafeRecordAsync(() => _audit.RecordAsync("volume", "delete", name, false, ex.Message, ElapsedMs(started))).ConfigureAwait(false);
            }
            throw;
        }
    }

    private static async Task SafeRecordAsync(Func<Task> record)
    {
        try
        {
            await record().ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static VolumeItem Parse(JsonElement e)
    {
        var name = JsonGet(e, "Name") ?? "";
        var driver = JsonGet(e, "Driver") ?? "";
        var mountpoint = JsonGet(e, "Mountpoint", "MountPoint");
        var scope = JsonGet(e, "Scope");
        return new VolumeItem(name, driver, mountpoint, scope);
    }

    private static string? JsonGet(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        }
        return null;
    }

    private static long ElapsedMs(DateTimeOffset started)
        => (long)(DateTimeOffset.Now - started).TotalMilliseconds;
}
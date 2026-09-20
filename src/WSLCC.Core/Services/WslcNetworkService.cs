using System.Text.Json;
using WSLCC.Core.Cli;
using WSLCC.Core.Models;

namespace WSLCC.Core.Services;

public interface INetworkService
{
    Task<IReadOnlyList<NetworkItem>> ListAsync(CancellationToken ct = default);
    Task CreateAsync(string name, string? driver, IReadOnlyList<string> options, IReadOnlyList<string> labels,
        string? subnet, string? gateway, string? ipRange, bool @internal, CancellationToken ct = default);
    Task RemoveAsync(string name, bool force = false, CancellationToken ct = default);
    Task ConnectAsync(string network, string container, IReadOnlyList<string> aliases, string? ip, CancellationToken ct = default);
    Task DisconnectAsync(string network, string container, bool force = false, CancellationToken ct = default);
    Task<string> PruneAsync(CancellationToken ct = default);
    Task<string> InspectAsync(string name, CancellationToken ct = default);
}

public sealed class WslcNetworkService : INetworkService
{
    private readonly WslcRunner _runner;
    private readonly IWslcAuditService _audit;

    public WslcNetworkService(WslcRunner runner, IWslcAuditService audit)
    {
        _runner = runner;
        _audit = audit;
    }

    public async Task<IReadOnlyList<NetworkItem>> ListAsync(CancellationToken ct = default)
    {
        var lines = await _runner.RunJsonLinesAsync(["network", "list", "--format", "json"], ct).ConfigureAwait(false);
        return lines.Select(Parse).ToList();
    }

    public async Task CreateAsync(string name, string? driver, IReadOnlyList<string> options, IReadOnlyList<string> labels,
        string? subnet, string? gateway, string? ipRange, bool @internal, CancellationToken ct = default)
    {
        var args = new List<string> { "network", "create" };
        if (!string.IsNullOrWhiteSpace(driver)) { args.Add("-d"); args.Add(driver.Trim()); }
        foreach (var option in options) { args.Add("-o"); args.Add(option); }
        foreach (var label in labels) { args.Add("-l"); args.Add(label); }
        if (!string.IsNullOrWhiteSpace(subnet)) { args.Add("--subnet"); args.Add(subnet.Trim()); }
        if (!string.IsNullOrWhiteSpace(gateway)) { args.Add("--gateway"); args.Add(gateway.Trim()); }
        if (!string.IsNullOrWhiteSpace(ipRange)) { args.Add("--ip-range"); args.Add(ipRange.Trim()); }
        if (@internal) args.Add("--internal");
        args.Add(name);
        await ExecuteAsync(args, "network", "create", name, ct).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string name, bool force = false, CancellationToken ct = default)
    {
        var args = force
            ? new List<string> { "network", "rm", "-f", name }
            : ["network", "rm", name];
        await ExecuteAsync(args, "network", "remove", name, ct).ConfigureAwait(false);
    }

    public async Task ConnectAsync(string network, string container, IReadOnlyList<string> aliases, string? ip, CancellationToken ct = default)
    {
        var args = new List<string> { "network", "connect" };
        foreach (var alias in aliases) { args.Add("--alias"); args.Add(alias); }
        if (!string.IsNullOrWhiteSpace(ip)) { args.Add("--ip"); args.Add(ip.Trim()); }
        args.Add(network);
        args.Add(container);
        await ExecuteAsync(args, "network", "connect", $"{network}/{container}", ct).ConfigureAwait(false);
    }

    public async Task DisconnectAsync(string network, string container, bool force = false, CancellationToken ct = default)
    {
        var args = new List<string> { "network", "disconnect" };
        if (force) args.Add("-f");
        args.Add(network);
        args.Add(container);
        await ExecuteAsync(args, "network", "disconnect", $"{network}/{container}", ct).ConfigureAwait(false);
    }

    public async Task<string> PruneAsync(CancellationToken ct = default)
    {
        var started = DateTimeOffset.Now;
        try
        {
            var output = await _runner.RunAsync(["network", "prune", "-f"], ct: ct).ConfigureAwait(false);
            await SafeRecordAsync(() => _audit.RecordAsync("network", "prune", "unused", true, durationMs: ElapsedMs(started))).ConfigureAwait(false);
            return string.Join(Environment.NewLine,
                output.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0));
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                await SafeRecordAsync(() => _audit.RecordAsync("network", "prune", "unused", false, ex.Message, ElapsedMs(started))).ConfigureAwait(false);
            throw;
        }
    }

    public Task<string> InspectAsync(string name, CancellationToken ct = default)
        => _runner.RunAsync(["network", "inspect", name], ct: ct);

    private async Task ExecuteAsync(IReadOnlyList<string> args, string category, string action, string target, CancellationToken ct)
    {
        var started = DateTimeOffset.Now;
        try
        {
            await _runner.RunAsync(args, ct: ct).ConfigureAwait(false);
            await SafeRecordAsync(() => _audit.RecordAsync(category, action, target, true, durationMs: ElapsedMs(started))).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                await SafeRecordAsync(() => _audit.RecordAsync(category, action, target, false, ex.Message, ElapsedMs(started))).ConfigureAwait(false);
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

    private static NetworkItem Parse(JsonElement e)
        => new(
            JsonGet(e, "Name", "name") ?? "",
            JsonGet(e, "ID", "Id", "id") ?? "",
            JsonGet(e, "Driver", "driver") ?? "",
            JsonGet(e, "Scope", "scope") ?? "",
            JsonGet(e, "IPv4", "ipv4") ?? "",
            JsonGet(e, "IPv6", "ipv6") ?? "",
            JsonGet(e, "Internal", "internal") ?? "",
            JsonGet(e, "Labels", "labels") ?? "",
            JsonGet(e, "CreatedAt", "createdAt"));

    private static string? JsonGet(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var v))
            {
                if (v.ValueKind == JsonValueKind.String) return v.GetString();
                if (v.ValueKind is JsonValueKind.True or JsonValueKind.False) return v.GetBoolean().ToString();
            }
        }
        return null;
    }

    private static long ElapsedMs(DateTimeOffset started)
        => (long)(DateTimeOffset.Now - started).TotalMilliseconds;
}

public interface IWslcPruneService
{
    Task<string> PruneContainersAsync(CancellationToken ct = default);
    Task<string> PruneImagesAsync(bool all = false, CancellationToken ct = default);
    Task<string> PruneVolumesAsync(CancellationToken ct = default);
    Task<string> PruneNetworksAsync(CancellationToken ct = default);
}

public sealed class WslcPruneService : IWslcPruneService
{
    private readonly WslcRunner _runner;
    private readonly IWslcAuditService _audit;

    public WslcPruneService(WslcRunner runner, IWslcAuditService audit)
    {
        _runner = runner;
        _audit = audit;
    }

    public Task<string> PruneContainersAsync(CancellationToken ct = default)
        => PruneAsync("container", ["container", "prune", "-f"], ct);

    public Task<string> PruneImagesAsync(bool all = false, CancellationToken ct = default)
        => PruneAsync("image", all ? ["image", "prune", "-a", "-f"] : ["image", "prune", "-f"], ct);

    public Task<string> PruneVolumesAsync(CancellationToken ct = default)
        => PruneAsync("volume", ["volume", "prune", "-f"], ct);

    public Task<string> PruneNetworksAsync(CancellationToken ct = default)
        => PruneAsync("network", ["network", "prune", "-f"], ct);

    private async Task<string> PruneAsync(string category, IReadOnlyList<string> args, CancellationToken ct)
    {
        var started = DateTimeOffset.Now;
        try
        {
            var output = await _runner.RunAsync(args, ct: ct).ConfigureAwait(false);
            await SafeRecordAsync(() => _audit.RecordAsync(category, "prune", "unused", true, durationMs: ElapsedMs(started))).ConfigureAwait(false);
            return Summarize(output);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                await SafeRecordAsync(() => _audit.RecordAsync(category, "prune", "unused", false, ex.Message, ElapsedMs(started))).ConfigureAwait(false);
            throw;
        }
    }

    private static string Summarize(string output)
        => string.Join(Environment.NewLine,
            output.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0));

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

    private static long ElapsedMs(DateTimeOffset started)
        => (long)(DateTimeOffset.Now - started).TotalMilliseconds;
}

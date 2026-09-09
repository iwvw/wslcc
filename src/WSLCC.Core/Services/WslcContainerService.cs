using System.Text;
using System.Text.Json;
using WSLCC.Core.Cli;
using WSLCC.Core.Models;

namespace WSLCC.Core.Services;

public interface IWslcContainerService
{
    Task<IReadOnlyList<ContainerItem>> ListAsync(CancellationToken ct = default);
    Task StartAsync(string nameOrId, CancellationToken ct = default);
    Task StopAsync(string nameOrId, CancellationToken ct = default);
    Task KillAsync(string nameOrId, CancellationToken ct = default);
    Task RestartAsync(string nameOrId, CancellationToken ct = default);
    Task RemoveAsync(string nameOrId, bool force = false, CancellationToken ct = default);
    Task RunAsync(ContainerCreateOptions options, IProgress<string>? progress = null, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, ContainerStats>> GetStatsAsync(CancellationToken ct = default);
}

public sealed class WslcContainerService : IWslcContainerService
{
    private readonly WslcRunner _runner;
    private readonly IWslcAuditService _audit;
    private readonly IWslcHistoryService _history;

    public WslcContainerService(WslcRunner runner, IWslcAuditService audit, IWslcHistoryService history)
    {
        _runner = runner;
        _audit = audit;
        _history = history;
    }

    public async Task<IReadOnlyList<ContainerItem>> ListAsync(CancellationToken ct = default)
    {
        var lines = await _runner.RunJsonLinesAsync("container list -a --format json", ct).ConfigureAwait(false);
        var items = lines.Select(Parse).ToList();
        await _history.RecordContainerSnapshotAsync(items).ConfigureAwait(false);
        return items;
    }

    public Task StartAsync(string nameOrId, CancellationToken ct = default)
        => ExecuteAsync("start", nameOrId, ct);

    public Task StopAsync(string nameOrId, CancellationToken ct = default)
        => ExecuteAsync("stop", nameOrId, ct);

    public Task KillAsync(string nameOrId, CancellationToken ct = default)
        => ExecuteAsync("kill", nameOrId, ct);

    public Task RestartAsync(string nameOrId, CancellationToken ct = default)
        => ExecuteAsync("restart", nameOrId, ct);

    public Task RemoveAsync(string nameOrId, bool force = false, CancellationToken ct = default)
        => ExecuteAsync($"rm {(force ? "-f " : "")}", nameOrId, ct, "remove");

    public Task RunAsync(ContainerCreateOptions options, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var args = new StringBuilder("run ");
        if (options.Detached) args.Append("-d ");
        if (options.AutoRemove) args.Append("--rm ");
        args.Append($"--name {Quote(options.Name)} ");
        foreach (var port in options.PortMappings)
            args.Append($"-p {Quote(port)} ");
        foreach (var env in options.EnvironmentVariables)
            args.Append($"-e {Quote(env)} ");
        foreach (var volume in options.Volumes)
            args.Append($"-v {Quote(volume)} ");
        if (options.Labels is { Count: > 0 })
        {
            foreach (var label in options.Labels)
                args.Append($"--label {Quote($"{label.Key}={label.Value}")} ");
        }
        args.Append(Quote(options.Image));
        if (options.Command is { Count: > 0 })
        {
            foreach (var cmd in options.Command)
                args.Append($" {Quote(cmd)}");
        }
        return ExecuteAsync(args.ToString(), options.Name, ct, "run", progress);
    }

    private async Task ExecuteAsync(string command, string target, CancellationToken ct, string? action = null, IProgress<string>? progress = null)
    {
        var started = DateTimeOffset.Now;
        action ??= command.Split(' ')[0];
        try
        {
            if (progress is null)
                await _runner.RunAsync($"{command} {Quote(target)}", ct: ct).ConfigureAwait(false);
            else
                await _runner.RunStreamingAsync($"{command} {Quote(target)}", progress, ct).ConfigureAwait(false);
            await _audit.RecordAsync("container", action, target, true, durationMs: ElapsedMs(started)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _audit.RecordAsync("container", action, target, false, ex.Message, ElapsedMs(started)).ConfigureAwait(false);
            throw;
        }
    }

    private static ContainerItem Parse(JsonElement e)
    {
        var names = JsonGet(e, "Names") ?? "";
        var name = names.Split(',')[0].TrimStart('/');
        return new ContainerItem(
            JsonGet(e, "ID", "Id") ?? "",
            name,
            JsonGet(e, "Image") ?? "",
            JsonGet(e, "State") ?? "",
            JsonGet(e, "Status") ?? "",
            JsonGet(e, "Ports") ?? "",
            JsonGet(e, "Command"),
            JsonGet(e, "CreatedAt"));
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

    public async Task<IReadOnlyDictionary<string, ContainerStats>> GetStatsAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<string, ContainerStats>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var lines = await _runner.RunJsonLinesAsync("stats --format json", ct).ConfigureAwait(false);
            foreach (var line in lines)
            {
                var name = JsonGet(line, "Name") ?? string.Empty;
                if (name.Length == 0) continue;
                var pids = 0;
                if (line.TryGetProperty("PIDs", out var pidEl) && pidEl.TryGetInt32(out var pidVal))
                    pids = pidVal;
                result[name] = new ContainerStats(
                    name,
                    JsonGet(line, "CPUPerc") ?? "-",
                    JsonGet(line, "MemPerc") ?? "-",
                    JsonGet(line, "MemUsage") ?? "-",
                    pids);
            }
        }
        catch
        {
        }
        return result;
    }

    private static string Quote(string value)
        => value.Contains(' ') ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
}
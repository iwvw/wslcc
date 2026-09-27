using System.Text.Json;
using WSLCC.Core.Cli;
using WSLCC.Core.Models;

namespace WSLCC.Core.Services;

public interface IWslcContainerService
{
    Task<IReadOnlyList<ContainerItem>> ListAsync(bool includeSize = false, CancellationToken ct = default);
    Task StartAsync(string nameOrId, CancellationToken ct = default);
    Task StopAsync(string nameOrId, CancellationToken ct = default);
    Task KillAsync(string nameOrId, CancellationToken ct = default);
    Task RestartAsync(string nameOrId, CancellationToken ct = default);
    Task RemoveAsync(string nameOrId, bool force = false, CancellationToken ct = default);
    Task RunAsync(ContainerCreateOptions options, IProgress<string>? progress = null, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, ContainerStats>> GetStatsAsync(CancellationToken ct = default);
    Task<ContainerItem?> InspectAsync(string nameOrId, bool includeSize = false, CancellationToken ct = default);
    Task CopyAsync(string source, string target, bool followLink = false, CancellationToken ct = default);
}

public sealed class WslcContainerService : IWslcContainerService
{
    private readonly WslcRunner _runner;
    private readonly IWslcAuditService _audit;
    private readonly IWslcHistoryService _history;
    private readonly IWslcCapabilities _capabilities;

    public WslcContainerService(
        WslcRunner runner, IWslcAuditService audit, IWslcHistoryService history, IWslcCapabilities capabilities)
    {
        _runner = runner;
        _audit = audit;
        _history = history;
        _capabilities = capabilities;
    }

    public async Task<IReadOnlyList<ContainerItem>> ListAsync(bool includeSize = false, CancellationToken ct = default)
    {
        var args = new List<string> { "container", "list", "-a" };
        if (includeSize) args.Add("--size");
        args.Add("--format");
        args.Add("json");
        var lines = await _runner.RunJsonLinesAsync(args, ct).ConfigureAwait(false);
        var items = lines.Select(Parse).ToList();
        await SafeRecordAsync(() => _history.RecordContainerSnapshotAsync(items)).ConfigureAwait(false);
        return items;
    }

    public Task StartAsync(string nameOrId, CancellationToken ct = default)
        => ExecuteAsync(["start", nameOrId], nameOrId, ct);

    public Task StopAsync(string nameOrId, CancellationToken ct = default)
        => ExecuteAsync(["stop", nameOrId], nameOrId, ct);

    public Task KillAsync(string nameOrId, CancellationToken ct = default)
        => ExecuteAsync(["kill", nameOrId], nameOrId, ct);

    public Task RestartAsync(string nameOrId, CancellationToken ct = default)
        => ExecuteAsync(["restart", nameOrId], nameOrId, ct);

    public Task RemoveAsync(string nameOrId, bool force = false, CancellationToken ct = default)
        => ExecuteAsync(
            force ? ["rm", "-f", nameOrId] : ["rm", nameOrId],
            nameOrId, ct, "remove");

    public Task RunAsync(ContainerCreateOptions options, IProgress<string>? progress = null, CancellationToken ct = default)
        => ExecuteAsync(BuildRunArgs(options), options.Name, ct, "run", progress);

    private static List<string> BuildRunArgs(ContainerCreateOptions options)
    {
        var args = new List<string> { "run" };
        if (options.Detached) args.Add("-d");
        if (options.AutoRemove) args.Add("--rm");
        args.Add("--name");
        args.Add(options.Name);
        foreach (var port in options.PortMappings)
        {
            args.Add("-p");
            args.Add(port);
        }
        foreach (var env in options.EnvironmentVariables)
        {
            args.Add("-e");
            args.Add(env);
        }
        foreach (var volume in options.Volumes)
        {
            args.Add("-v");
            args.Add(volume);
        }
        if (options.Labels is { Count: > 0 })
        {
            foreach (var label in options.Labels)
            {
                args.Add("--label");
                args.Add($"{label.Key}={label.Value}");
            }
        }
        AddValue(args, "--cpus", options.Cpus);
        AddValue(args, "-m", options.Memory);
        AddValue(args, "-h", options.Hostname);
        AddValue(args, "-w", options.Workdir);
        AddValue(args, "-u", options.User);
        AddValue(args, "--entrypoint", options.Entrypoint);
        AddValue(args, "--network", options.Network);
        AddValue(args, "--stop-signal", options.StopSignal);
        AddValue(args, "--stop-timeout", options.StopTimeout);
        AddValue(args, "--shm-size", options.ShmSize);
        AddValue(args, "--tmpfs", options.Tmpfs);
        AddValue(args, "--pull", options.PullPolicy);
        AddValue(args, "--health-cmd", options.HealthCommand);
        AddValue(args, "--health-interval", options.HealthInterval);
        AddValue(args, "--health-timeout", options.HealthTimeout);
        AddValue(args, "--health-retries", options.HealthRetries);
        AddValue(args, "--health-start-period", options.HealthStartPeriod);
        if (options.Gpus)
        {
            args.Add("--gpus");
            args.Add("all");
        }
        if (options.Dns is { Count: > 0 })
        {
            foreach (var dns in options.Dns)
            {
                args.Add("--dns");
                args.Add(dns);
            }
        }
        if (options.Ulimits is { Count: > 0 })
        {
            foreach (var ulimit in options.Ulimits)
            {
                args.Add("--ulimit");
                args.Add(ulimit);
            }
        }
        args.Add(options.Image);
        if (options.Command is { Count: > 0 })
            args.AddRange(options.Command);
        return args;
    }

    private static void AddValue(List<string> args, string flag, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        args.Add(flag);
        args.Add(value.Trim());
    }

    public async Task<IReadOnlyDictionary<string, ContainerStats>> GetStatsAsync(CancellationToken ct = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
        var lines = await _runner.RunJsonLinesAsync(["stats", "--format", "json"], timeoutCts.Token).ConfigureAwait(false);

        var result = new Dictionary<string, ContainerStats>(StringComparer.OrdinalIgnoreCase);
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
        return result;
    }

    public async Task<ContainerItem?> InspectAsync(string nameOrId, bool includeSize = false, CancellationToken ct = default)
    {
        try
        {
            var args = includeSize
                ? new List<string> { "inspect", "-s", nameOrId }
                : ["inspect", nameOrId];
            var json = await _runner.RunAsync(args, ct: ct).ConfigureAwait(false);
            return ParseInspect(nameOrId, json);
        }
        catch (WslcCliException)
        {
            return null;
        }
    }

    public async Task CopyAsync(string source, string target, bool followLink = false, CancellationToken ct = default)
    {
        var started = DateTimeOffset.Now;
        var args = new List<string> { "container", "cp" };
        if (followLink && await _capabilities.SupportsAsync(WslcFeature.CopyFollowLink, ct).ConfigureAwait(false))
            args.Add("--follow-link");
        args.Add(source);
        args.Add(target);
        try
        {
            await _runner.RunAsync(args, ct: ct).ConfigureAwait(false);
            await SafeRecordAsync(() => _audit.RecordAsync("container", "cp", $"{source} -> {target}", true, durationMs: ElapsedMs(started))).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                await SafeRecordAsync(() => _audit.RecordAsync("container", "cp", $"{source} -> {target}", false, ex.Message, ElapsedMs(started))).ConfigureAwait(false);
            throw;
        }
    }

    private static ContainerItem? ParseInspect(string fallbackName, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                root = root[0];

            var name = (JsonGet(root, "Name", "name") ?? fallbackName).TrimStart('/');
            var id = JsonGet(root, "ID", "Id") ?? "";
            var image = "";
            if (root.TryGetProperty("Config", out var config) && config.ValueKind == JsonValueKind.Object)
                image = JsonGet(config, "Image") ?? "";
            if (image.Length == 0)
                image = JsonGet(root, "Image") ?? "";

            var state = "";
            var status = "";
            if (root.TryGetProperty("State", out var stateEl) && stateEl.ValueKind == JsonValueKind.Object)
            {
                state = JsonGet(stateEl, "Status") ?? "";
                var exitCode = 0;
                if (stateEl.TryGetProperty("ExitCode", out var ec) && ec.TryGetInt32(out var ecv))
                    exitCode = ecv;
                status = state.Equals("running", StringComparison.OrdinalIgnoreCase)
                    ? "Up"
                    : $"Exited ({exitCode})";
            }

            return new ContainerItem(id, name, image, state, status, ParseInspectPorts(root), null, null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ParseInspectPorts(JsonElement root)
    {
        if (!root.TryGetProperty("NetworkSettings", out var ns) || ns.ValueKind != JsonValueKind.Object)
            return "";
        if (!ns.TryGetProperty("Ports", out var portsEl) || portsEl.ValueKind != JsonValueKind.Object)
            return "";
        var parts = new List<string>();
        foreach (var port in portsEl.EnumerateObject())
        {
            if (port.Value.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var binding in port.Value.EnumerateArray())
            {
                if (binding.ValueKind != JsonValueKind.Object)
                    continue;
                var hostIp = JsonGet(binding, "HostIp") ?? "";
                var hostPort = JsonGet(binding, "HostPort") ?? "";
                parts.Add($"{hostIp}:{hostPort}->{port.Name}");
            }
        }
        return string.Join(", ", parts);
    }

    private async Task ExecuteAsync(
        IReadOnlyList<string> args, string target, CancellationToken ct, string? action = null, IProgress<string>? progress = null)
    {
        var started = DateTimeOffset.Now;
        action ??= args.FirstOrDefault() ?? "container";
        try
        {
            if (progress is null)
                await _runner.RunAsync(args, ct: ct).ConfigureAwait(false);
            else
                await _runner.RunStreamingAsync(args, null, progress, ct).ConfigureAwait(false);
            await SafeRecordAsync(() => _audit.RecordAsync("container", action, target, true, durationMs: ElapsedMs(started))).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
            {
                await SafeRecordAsync(() => _audit.RecordAsync("container", action, target, false, ex.Message, ElapsedMs(started))).ConfigureAwait(false);
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
}
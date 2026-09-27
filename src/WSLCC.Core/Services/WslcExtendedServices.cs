using System.Runtime.InteropServices;
using WSLCC.Core.Cli;
using WSLCC.Core.Models;
using YamlDotNet.RepresentationModel;

namespace WSLCC.Core.Services;

public sealed record WslcLogOptions(
    int? Tail = null,
    bool Timestamps = false,
    bool Details = false,
    string? Since = null,
    string? Until = null);

public interface IWslcLogService
{
    Task<string> GetLogsAsync(string nameOrId, WslcLogOptions? options = null, CancellationToken ct = default);
}

public sealed class WslcLogService : IWslcLogService
{
    private readonly WslcRunner _runner;

    public WslcLogService(WslcRunner runner) => _runner = runner;

    public Task<string> GetLogsAsync(string nameOrId, WslcLogOptions? options = null, CancellationToken ct = default)
        => GetCleanLogsAsync(nameOrId, options, ct);

    private async Task<string> GetCleanLogsAsync(string nameOrId, WslcLogOptions? options, CancellationToken ct)
    {
        var args = new List<string> { "logs" };
        if (options is not null)
        {
            if (options.Tail is int tail) { args.Add("-n"); args.Add(tail.ToString()); }
            if (options.Timestamps) args.Add("--timestamps");
            if (options.Details) args.Add("--details");
            if (!string.IsNullOrWhiteSpace(options.Since)) { args.Add("--since"); args.Add(options.Since.Trim()); }
            if (!string.IsNullOrWhiteSpace(options.Until)) { args.Add("--until"); args.Add(options.Until.Trim()); }
        }
        args.Add(nameOrId);
        var raw = await _runner.RunAsync(
            args, new WslcRunner.RunOptions(CheckOutputForErrors: false), ct).ConfigureAwait(false);
        return AnsiText.Strip(raw);
    }
}

public interface IWslcInspectService
{
    Task<string> InspectContainerAsync(string nameOrId, bool includeSize = false, CancellationToken ct = default);
    Task<string> InspectImageAsync(string nameOrId, CancellationToken ct = default);
}

public sealed class WslcInspectService : IWslcInspectService
{
    private readonly WslcRunner _runner;

    public WslcInspectService(WslcRunner runner) => _runner = runner;

    public Task<string> InspectContainerAsync(string nameOrId, bool includeSize = false, CancellationToken ct = default)
        => _runner.RunAsync(includeSize ? ["inspect", "-s", nameOrId] : ["inspect", nameOrId], ct: ct);

    public Task<string> InspectImageAsync(string nameOrId, CancellationToken ct = default)
        => _runner.RunAsync(["image", "inspect", nameOrId], ct: ct);
}

public interface IWslcSystemService
{
    Task<IReadOnlyList<WslcSessionInfo>> ListSessionsAsync(CancellationToken ct = default);
    Task TerminateSessionsAsync(CancellationToken ct = default);
    Task<ResourceQuota> GetResourceQuotaAsync(CancellationToken ct = default);
}

public sealed class WslcSystemService : IWslcSystemService
{
    private readonly WslcRunner _runner;
    private readonly IWslcSettingsFileService _settingsFile;

    public WslcSystemService(WslcRunner runner, IWslcSettingsFileService settingsFile)
    {
        _runner = runner;
        _settingsFile = settingsFile;
    }

    public async Task<IReadOnlyList<WslcSessionInfo>> ListSessionsAsync(CancellationToken ct = default)
    {
        var output = await _runner.RunAsync(["system", "session", "list"], ct: ct).ConfigureAwait(false);
        var sessions = new List<WslcSessionInfo>();
        foreach (var line in output.Split('\n'))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            if (!uint.TryParse(parts[0], out var id)) continue;
            uint? pid = null;
            if (parts.Length > 1 && uint.TryParse(parts[1], out var p)) pid = p;
            var name = parts.Length > 2 ? string.Join(" ", parts.Skip(2)) : string.Empty;
            sessions.Add(new WslcSessionInfo(id, name, pid));
        }
        return sessions;
    }

    public Task TerminateSessionsAsync(CancellationToken ct = default)
        => _runner.RunAsync(["system", "session", "terminate"], ct: ct);

    public async Task<ResourceQuota> GetResourceQuotaAsync(CancellationToken ct = default)
    {
        var settings = _settingsFile.Read();
        var cpu = string.IsNullOrEmpty(settings.CpuCount) || settings.CpuCount == WslcSettingsFileService.DefaultSentinel
            ? "全部核心"
            : settings.CpuCount;
        var storage = string.IsNullOrEmpty(settings.MaxStorageSize) || settings.MaxStorageSize == WslcSettingsFileService.DefaultSentinel
            ? "1 TB"
            : settings.MaxStorageSize;
        var memory = string.IsNullOrEmpty(settings.MemorySize) || settings.MemorySize == WslcSettingsFileService.DefaultSentinel
            ? $"物理内存一半（约 {Math.Max(GetTotalPhysicalMemoryGb() / 2, 1)} GB）"
            : settings.MemorySize;
        return new ResourceQuota(cpu, memory, storage);
    }

    private static long GetTotalPhysicalMemoryGb()
    {
        try
        {
            var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref status))
                return (long)(status.ullTotalPhys / (1024.0 * 1024 * 1024));
        }
        catch
        {
        }
        return 0;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}

public sealed record WslcEventFilter(string Key, IReadOnlyList<string> Values);

public sealed record WslcEventOptions(
    string? Since = null,
    string? Until = null,
    IReadOnlyList<WslcEventFilter>? Filters = null);

public sealed record WslcEventEntry(string Timestamp, string Type, string Action, string Target, string Attributes)
{
    public string TimeText => Timestamp.Length >= 19 ? Timestamp[..19].Replace('T', ' ') : Timestamp;

    public string Summary => Attributes.Length == 0
        ? $"{Type} {Action} {Target}"
        : $"{Type} {Action} {Target} ({Attributes})";

    public static WslcEventEntry? TryParse(string line)
    {
        var trimmed = line.TrimEnd('\r', '\n');
        if (trimmed.Length == 0) return null;
        var space = trimmed.IndexOf(' ');
        if (space <= 0) return null;

        var timestamp = trimmed[..space];
        var rest = trimmed[(space + 1)..].Trim();

        var attributes = string.Empty;
        var paren = rest.IndexOf('(');
        if (paren >= 0)
        {
            var close = rest.LastIndexOf(')');
            if (close > paren)
            {
                attributes = rest[(paren + 1)..close];
                rest = rest[..paren].TrimEnd();
            }
        }

        var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;
        var type = parts[0];
        var action = parts[1];
        var target = parts.Length > 2 ? parts[2] : string.Empty;
        return new WslcEventEntry(timestamp, type, action, target, attributes);
    }
}

public interface IWslcEventService
{
    Task<bool> IsSupportedAsync(CancellationToken ct = default);

    Task<IReadOnlyList<WslcEventEntry>> QueryAsync(WslcEventOptions? options = null, CancellationToken ct = default);

    Task StreamAsync(
        WslcEventOptions? options, IProgress<WslcEventEntry> progress, CancellationToken ct = default);
}

public sealed class WslcEventService : IWslcEventService
{
    private readonly WslcRunner _runner;
    private readonly IWslcCapabilities _capabilities;

    public WslcEventService(WslcRunner runner, IWslcCapabilities capabilities)
    {
        _runner = runner;
        _capabilities = capabilities;
    }

    public Task<bool> IsSupportedAsync(CancellationToken ct = default)
        => _capabilities.SupportsAsync(WslcFeature.Events, ct);

    public async Task<IReadOnlyList<WslcEventEntry>> QueryAsync(
        WslcEventOptions? options = null, CancellationToken ct = default)
    {
        var output = await _runner.RunAsync(BuildArgs(options, boundUntil: true), ct: ct).ConfigureAwait(false);
        var entries = new List<WslcEventEntry>();
        foreach (var line in output.Split('\n'))
        {
            var entry = WslcEventEntry.TryParse(line);
            if (entry is not null) entries.Add(entry);
        }
        return entries;
    }

    public Task StreamAsync(
        WslcEventOptions? options, IProgress<WslcEventEntry> progress, CancellationToken ct = default)
    {
        var lineProgress = new Progress<string>(line =>
        {
            var entry = WslcEventEntry.TryParse(line);
            if (entry is not null) progress.Report(entry);
        });
        return _runner.RunStreamingAsync(
            BuildArgs(options, boundUntil: false), new WslcRunner.RunOptions(CheckOutputForErrors: false), lineProgress, ct);
    }

    private static List<string> BuildArgs(WslcEventOptions? options, bool boundUntil)
    {
        var args = new List<string> { "events" };
        var since = options?.Since;
        var until = options?.Until;
        if (!string.IsNullOrWhiteSpace(since)) { args.Add("--since"); args.Add(since.Trim()); }
        if (!string.IsNullOrWhiteSpace(until))
        {
            args.Add("--until");
            args.Add(until.Trim());
        }
        else if (boundUntil)
        {
            args.Add("--until");
            args.Add((DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1).ToString());
        }
        if (options?.Filters is not null)
        {
            foreach (var filter in options.Filters)
            {
                foreach (var value in filter.Values)
                {
                    args.Add("--filter");
                    args.Add($"{filter.Key}={value}");
                }
            }
        }
        return args;
    }
}
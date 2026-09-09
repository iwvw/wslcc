using System.Runtime.InteropServices;
using WSLCC.Core.Cli;
using WSLCC.Core.Models;
using YamlDotNet.RepresentationModel;

namespace WSLCC.Core.Services;

public interface IWslcLogService
{
    Task<string> GetLogsAsync(string nameOrId, CancellationToken ct = default);
}

public sealed class WslcLogService : IWslcLogService
{
    private readonly WslcRunner _runner;

    public WslcLogService(WslcRunner runner) => _runner = runner;

    public Task<string> GetLogsAsync(string nameOrId, CancellationToken ct = default)
        => GetCleanLogsAsync(nameOrId, ct);

    private async Task<string> GetCleanLogsAsync(string nameOrId, CancellationToken ct)
    {
        var raw = await _runner.RunAsync(
            ["logs", nameOrId], new WslcRunner.RunOptions(CheckOutputForErrors: false), ct).ConfigureAwait(false);
        return AnsiText.Strip(raw);
    }
}

public interface IWslcInspectService
{
    Task<string> InspectContainerAsync(string nameOrId, CancellationToken ct = default);
}

public sealed class WslcInspectService : IWslcInspectService
{
    private readonly WslcRunner _runner;

    public WslcInspectService(WslcRunner runner) => _runner = runner;

    public Task<string> InspectContainerAsync(string nameOrId, CancellationToken ct = default)
        => _runner.RunAsync(["inspect", nameOrId], ct: ct);
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

    public WslcSystemService(WslcRunner runner) => _runner = runner;

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
        var cpu = "全部核心";
        var memory = string.Empty;
        var storage = "1 TB";

        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "wslc", "settings.yaml");
            if (File.Exists(settingsPath))
            {
                var yaml = new YamlStream();
                using var reader = new StreamReader(settingsPath);
                yaml.Load(reader);
                if (yaml.Documents.Count > 0
                    && yaml.Documents[0].RootNode is YamlMappingNode root
                    && root.Children.TryGetValue(new YamlScalarNode("session"), out var sessionNode)
                    && sessionNode is YamlMappingNode session)
                {
                    var configuredCpu = GetScalar(session, "cpuCount");
                    if (!string.IsNullOrEmpty(configuredCpu) && configuredCpu != "default")
                        cpu = configuredCpu;
                    var configuredMemory = GetScalar(session, "memorySize");
                    if (!string.IsNullOrEmpty(configuredMemory) && configuredMemory != "default")
                        memory = configuredMemory;
                    var configuredStorage = GetScalar(session, "maxStorageSize");
                    if (!string.IsNullOrEmpty(configuredStorage) && configuredStorage != "default")
                        storage = configuredStorage;
                }
            }
        }
        catch
        {
        }

        if (string.IsNullOrEmpty(memory))
        {
            var totalGb = GetTotalPhysicalMemoryGb();
            memory = $"物理内存一半（约 {Math.Max(totalGb / 2, 1)} GB）";
        }

        return new ResourceQuota(cpu, memory, storage);
    }

    private static string? GetScalar(YamlMappingNode map, string key)
    {
        if (!map.Children.TryGetValue(new YamlScalarNode(key), out var node)) return null;
        return node is YamlScalarNode scalar ? scalar.Value : null;
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
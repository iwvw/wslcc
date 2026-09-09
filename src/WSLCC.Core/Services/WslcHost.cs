using WSLCC.Core.Cli;
using WSLCC.Data;

namespace WSLCC.Core.Services;

public sealed class WslcHost
{
    public WslcRunner Runner { get; }

    public WslcDatabase Database { get; }

    public IWslcEnvironmentService Environment { get; }

    public IWslcImageService Images { get; }

    public IWslcContainerService Containers { get; }

    public IWslcApiHost Api { get; }

    public IWslcLogService Logs { get; }

    public IWslcInspectService Inspect { get; }

    public IWslcSystemService System { get; }

    public IWslcComposeService Compose { get; }

    public IWslcVolumeService Volumes { get; }

    public IWslcSettingsService Settings { get; }

    public IWslcAuditService Audit { get; }

    public IWslcHistoryService History { get; }

    public const string SessionName = "wslcc-desktop";

    public static string DefaultStoragePath
        => Path.Combine(global::System.Environment.GetFolderPath(global::System.Environment.SpecialFolder.LocalApplicationData), "WslcData");

    public WslcHost(string? databasePath = null)
    {
        Runner = new WslcRunner();
        Database = string.IsNullOrEmpty(databasePath) ? WslcDatabase.OpenDefault() : new WslcDatabase(databasePath);

        var settingsRepository = new SettingsRepository(Database);
        var auditRepository = new AuditLogRepository(Database);
        var pullHistoryRepository = new PullHistoryRepository(Database);
        var snapshotRepository = new ContainerSnapshotRepository(Database);
        var composeDeployments = new ComposeDeploymentRepository(Database);

        Environment = new WslcEnvironmentService(Runner);
        Api = new WslcApiHost();
        Audit = new WslcAuditService(auditRepository);
        Settings = new WslcSettingsService(settingsRepository);
        History = new WslcHistoryService(pullHistoryRepository, snapshotRepository);
        Images = new WslcImageService(Runner, Api, Audit, History);
        Containers = new WslcContainerService(Runner, Audit, History);
        Logs = new WslcLogService(Runner);
        Inspect = new WslcInspectService(Runner);
        System = new WslcSystemService(Runner);
        Compose = new WslcComposeService(Containers, Audit, composeDeployments);
        Volumes = new WslcVolumeService(Runner, Audit);
    }

    public async Task InitializeAsync()
        => await Database.InitializeAsync();

    public static WslcHost Default { get; } = new();
}
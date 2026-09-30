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

    public IWslcEventService Events { get; }

    public IWslcCapabilities Capabilities { get; }

    public IWslcComposeService Compose { get; }

    public IWslcVolumeService Volumes { get; }

    public IWslcSettingsService Settings { get; }

    public IWslcAuditService Audit { get; }

    public IWslcHistoryService History { get; }

    public IWslcUpdateService Update { get; }

    public IWslcInstallService Install { get; }

    public IWslcStartupService Startup { get; }

    public IWslcSettingsFileService SettingsFile { get; }

    public INetworkService Networks { get; }

    public IWslcPruneService Prune { get; }

    public const string SessionName = "";

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

        SettingsFile = new WslcSettingsFileService();
        Capabilities = new WslcCapabilities(Runner);
        Environment = new WslcEnvironmentService(Runner);
        Api = new WslcApiHost();
        Audit = new WslcAuditService(auditRepository);
        Settings = new WslcSettingsService(settingsRepository, SettingsFile);
        History = new WslcHistoryService(pullHistoryRepository, snapshotRepository);
        Images = new WslcImageService(Runner, Audit, History, Capabilities);
        Containers = new WslcContainerService(Runner, Audit, History, Capabilities);
        Logs = new WslcLogService(Runner);
        Inspect = new WslcInspectService(Runner);
        System = new WslcSystemService(Runner, SettingsFile);
        Events = new WslcEventService(Runner, Capabilities);
        Compose = new WslcComposeService(Containers, Audit, composeDeployments, Settings, Images);
        Volumes = new WslcVolumeService(Runner, Audit);
        Networks = new WslcNetworkService(Runner, Audit);
        Prune = new WslcPruneService(Runner, Audit);
        Update = new WslcUpdateService();
        Install = new WslcInstallService(Runner);
        Startup = new WslcStartupService();
    }

    public async Task InitializeAsync()
        => await Database.InitializeAsync();

    public static WslcHost Default { get; } = new();
}
namespace WSLCC.Core.Models;

public sealed record WslcClientInfo(string Version, string WindowsVersion, string KernelVersion, string SettingsFile);

public sealed record WslcSessionInfo(uint Id, string Name, uint? CreatorPid);

public sealed record WslcServerInfo(string SessionManagerVersion, IReadOnlyList<WslcSessionInfo> Sessions);

public sealed record WslcEnvironmentInfo(
    WslcClientInfo? Client,
    WslcServerInfo? Server,
    IReadOnlyList<string> MissingComponents,
    bool ApiAvailable);

public sealed record ResourceQuota(string Cpu, string Memory, string Storage);

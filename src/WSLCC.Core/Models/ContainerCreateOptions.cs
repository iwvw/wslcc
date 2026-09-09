namespace WSLCC.Core.Models;

public sealed record ContainerCreateOptions(
    string Name,
    string Image,
    IReadOnlyList<string> PortMappings,
    IReadOnlyList<string> EnvironmentVariables,
    IReadOnlyList<string> Volumes,
    bool AutoRemove,
    bool Detached,
    IReadOnlyList<string>? Command = null,
    IReadOnlyDictionary<string, string>? Labels = null);

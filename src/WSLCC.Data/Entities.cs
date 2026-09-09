namespace WSLCC.Data;

public sealed record AuditLogEntry(
    long Id,
    DateTimeOffset Timestamp,
    string Category,
    string Action,
    string? Detail,
    string Result,
    string? Message,
    long? DurationMs);

public sealed record PullHistoryEntry(
    long Id,
    string ImageRef,
    string Status,
    string? Error,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);

public sealed record ContainerSnapshotEntry(
    string Name,
    string? Image,
    string? State,
    string? Status,
    string? Ports,
    DateTimeOffset ObservedAt);

public sealed record SessionStateEntry(
    string Name,
    string StoragePath,
    int? CpuCount,
    int? MemoryMb,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? LastStartedAt,
    DateTimeOffset? LastTerminatedAt);
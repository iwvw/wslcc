namespace WSLCC.Core.Models;

public sealed record ContainerItem(
    string Id,
    string Name,
    string Image,
    string State,
    string Status,
    string Ports,
    string? Command,
    string? CreatedAt);

public sealed record ContainerStats(
    string Name,
    string CpuPerc,
    string MemPerc,
    string MemUsage,
    int Pids);

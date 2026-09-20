namespace WSLCC.Core.Models;

public sealed record NetworkItem(
    string Name,
    string Id,
    string Driver,
    string Scope,
    string IPv4,
    string IPv6,
    string Internal,
    string Labels,
    string? CreatedAt);

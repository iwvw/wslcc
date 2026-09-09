using WSLCC.Data;

namespace WSLCC.Core.Services;

public interface IWslcAuditService
{
    Task RecordAsync(string category, string action, string? detail, bool success, string? message = null, long? durationMs = null);
    Task<IReadOnlyList<AuditLogEntry>> QueryAsync(int limit = 200);
    Task<long> CountAsync();
    Task ClearAsync();
}

public sealed class WslcAuditService : IWslcAuditService
{
    private readonly AuditLogRepository _repository;

    public WslcAuditService(AuditLogRepository repository) => _repository = repository;

    public Task RecordAsync(string category, string action, string? detail, bool success, string? message = null, long? durationMs = null)
        => _repository.AddAsync(category, action, detail, success, message, durationMs);

    public Task<IReadOnlyList<AuditLogEntry>> QueryAsync(int limit = 200) => _repository.QueryAsync(limit);

    public Task<long> CountAsync() => _repository.CountAsync();

    public Task ClearAsync() => _repository.ClearAsync();
}
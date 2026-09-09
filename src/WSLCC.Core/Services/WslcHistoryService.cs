using WSLCC.Data;
using WSLCC.Core.Models;

namespace WSLCC.Core.Services;

public interface IWslcHistoryService
{
    Task RecordPullAsync(string imageRef, bool success, string? error, DateTimeOffset startedAt, DateTimeOffset? finishedAt);
    Task<IReadOnlyList<PullHistoryEntry>> QueryPullsAsync(int limit = 100);
    Task RecordContainerSnapshotAsync(IReadOnlyList<ContainerItem> items);
    Task<IReadOnlyList<ContainerSnapshotEntry>> QueryLatestSnapshotsAsync(int limit = 100);
}

public sealed class WslcHistoryService : IWslcHistoryService
{
    private readonly PullHistoryRepository _pullHistory;
    private readonly ContainerSnapshotRepository _snapshots;

    public WslcHistoryService(PullHistoryRepository pullHistory, ContainerSnapshotRepository snapshots)
    {
        _pullHistory = pullHistory;
        _snapshots = snapshots;
    }

    public Task RecordPullAsync(string imageRef, bool success, string? error, DateTimeOffset startedAt, DateTimeOffset? finishedAt)
        => _pullHistory.AddAsync(imageRef, success, error, startedAt, finishedAt);

    public Task<IReadOnlyList<PullHistoryEntry>> QueryPullsAsync(int limit = 100)
        => _pullHistory.QueryAsync(limit);

    public Task RecordContainerSnapshotAsync(IReadOnlyList<ContainerItem> items)
    {
        var now = DateTimeOffset.Now;
        return _snapshots.AddRangeAsync(items.Select(i => new ContainerSnapshotEntry(
            i.Name, i.Image, i.State, i.Status, i.Ports, now)));
    }

    public Task<IReadOnlyList<ContainerSnapshotEntry>> QueryLatestSnapshotsAsync(int limit = 100)
        => _snapshots.QueryLatestAsync(limit);
}
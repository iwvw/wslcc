using System.Diagnostics;
using System.Text.Json;
using WSLCC.Core.Cli;
using WSLCC.Core.Models;

namespace WSLCC.Core.Services;

public interface IWslcImageService
{
    Task<IReadOnlyList<ImageItem>> ListAsync(
        bool includeIntermediate = false, bool includeDigests = false, CancellationToken ct = default);
    Task PullAsync(
        string imageRef, bool allTags = false, IProgress<string>? progress = null, CancellationToken ct = default);
    Task DeleteAsync(string imageRef, CancellationToken ct = default);
    Task<string> InspectAsync(string imageRef, CancellationToken ct = default);
}

public sealed class WslcImageService : IWslcImageService
{
    private readonly WslcRunner _runner;
    private readonly IWslcAuditService _audit;
    private readonly IWslcHistoryService _history;
    private readonly IWslcCapabilities _capabilities;

    public WslcImageService(
        WslcRunner runner, IWslcAuditService audit, IWslcHistoryService history, IWslcCapabilities capabilities)
    {
        _runner = runner;
        _audit = audit;
        _history = history;
        _capabilities = capabilities;
    }

    public async Task<IReadOnlyList<ImageItem>> ListAsync(
        bool includeIntermediate = false, bool includeDigests = false, CancellationToken ct = default)
    {
        var args = new List<string> { "image", "ls" };
        if (includeIntermediate) args.Add("--all");
        if (includeDigests && await _capabilities.SupportsAsync(WslcFeature.ImageDigests, ct).ConfigureAwait(false))
            args.Add("--digests");
        args.Add("--format");
        args.Add("json");
        var lines = await _runner.RunJsonLinesAsync(args, ct).ConfigureAwait(false);
        return lines.Select(Parse).ToList();
    }

    public Task<string> InspectAsync(string imageRef, CancellationToken ct = default)
        => _runner.RunAsync(["image", "inspect", imageRef], ct: ct);

    public async Task PullAsync(
        string imageRef, bool allTags = false, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var started = DateTimeOffset.Now;
        var args = new List<string> { "pull" };
        if (allTags && await _capabilities.SupportsAsync(WslcFeature.PullAllTags, ct).ConfigureAwait(false))
            args.Add("--all-tags");
        args.Add(imageRef);
        try
        {
            await _runner.RunStreamingAsync(args, null, progress, ct).ConfigureAwait(false);
            await SafeRecordAsync(async () =>
            {
                await _audit.RecordAsync("image", "pull", imageRef, true, durationMs: ElapsedMs(started)).ConfigureAwait(false);
                await _history.RecordPullAsync(imageRef, true, null, started, DateTimeOffset.Now).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
            {
                await SafeRecordAsync(async () =>
                {
                    await _audit.RecordAsync("image", "pull", imageRef, false, ex.Message, ElapsedMs(started)).ConfigureAwait(false);
                    await _history.RecordPullAsync(imageRef, false, ex.Message, started, DateTimeOffset.Now).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
            throw;
        }
    }

    public async Task DeleteAsync(string imageRef, CancellationToken ct = default)
    {
        var started = DateTimeOffset.Now;
        try
        {
            await _runner.RunAsync(["rmi", imageRef], ct: ct).ConfigureAwait(false);
            await SafeRecordAsync(() => _audit.RecordAsync("image", "delete", imageRef, true, durationMs: ElapsedMs(started))).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
            {
                await SafeRecordAsync(() => _audit.RecordAsync("image", "delete", imageRef, false, ex.Message, ElapsedMs(started))).ConfigureAwait(false);
            }
            throw;
        }
    }

    private static async Task SafeRecordAsync(Func<Task> record)
    {
        try
        {
            await record().ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static ImageItem Parse(JsonElement e)
    {
        var id = JsonGet(e, "ID", "Id", "ImageID") ?? "";
        if (id.StartsWith("sha256:", StringComparison.Ordinal)) id = id[7..];
        return new ImageItem(
            JsonGet(e, "Repository", "RepoName", "Name") ?? "<none>",
            JsonGet(e, "Tag") ?? "<none>",
            id,
            JsonGet(e, "Size") ?? "",
            JsonGet(e, "CreatedAt"),
            JsonGet(e, "CreatedSince"),
            JsonGet(e, "Digest"));
    }

    private static string? JsonGet(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        }
        return null;
    }

    private static long ElapsedMs(DateTimeOffset started)
        => (long)(DateTimeOffset.Now - started).TotalMilliseconds;
}

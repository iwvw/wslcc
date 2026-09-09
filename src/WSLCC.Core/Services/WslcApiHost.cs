using Microsoft.WSL.Containers;
using WSLCC.Core.Models;

namespace WSLCC.Core.Services;

public interface IWslcApiHost
{
    bool TryGetVersion(out string version);
    IReadOnlyList<string> GetMissingComponents();
    Task EnsureSessionAsync(string sessionName, string storagePath, CancellationToken ct = default);
    Task PullImageAsync(string uri, IProgress<ImagePullProgress>? progress = null, CancellationToken ct = default);
    Task<IReadOnlyList<ImageItem>> GetImagesAsync(CancellationToken ct = default);
    Task DeleteImageAsync(string nameOrId, CancellationToken ct = default);
    Task TerminateSessionAsync(CancellationToken ct = default);
}

public sealed record ImagePullProgress(string? ImageId, string Status, ulong CurrentBytes, ulong TotalBytes);

public sealed class WslcApiHost : IWslcApiHost
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Session? _session;

    public bool TryGetVersion(out string version)
    {
        try
        {
            var v = WslcService.GetVersion();
            version = $"{v.Major}.{v.Minor}.{v.Revision}";
            return true;
        }
        catch
        {
            version = string.Empty;
            return false;
        }
    }

    public IReadOnlyList<string> GetMissingComponents()
    {
        try
        {
            return WslcService.GetMissingComponents().Select(c => c.ToString()).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public async Task EnsureSessionAsync(string sessionName, string storagePath, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_session is not null) return;
            Directory.CreateDirectory(storagePath);
            var settings = new SessionSettings(sessionName, storagePath);
            var session = new Session(settings);
            session.Start();
            _session = session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PullImageAsync(string uri, IProgress<ImagePullProgress>? progress = null, CancellationToken ct = default)
    {
        var session = await GetSessionAsync(ct).ConfigureAwait(false);
        var operation = session.PullImageAsync(new PullImageOptions(uri));
        if (progress is not null)
        {
            operation.Progress = (_, p) => progress.Report(new ImagePullProgress(
                string.IsNullOrEmpty(p.Id) ? null : p.Id,
                p.Status.ToString(),
                p.CurrentBytes,
                p.TotalBytes));
        }
        await operation.AsTask(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ImageItem>> GetImagesAsync(CancellationToken ct = default)
    {
        var session = await GetSessionAsync(ct).ConfigureAwait(false);
        return await Task.Run(() =>
        {
            var list = session.GetImages();
            return list.Select(i => new ImageItem(
                i.Name,
                string.Empty,
                string.Empty,
                i.Size.ToString(),
                i.CreatedTimestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"))).ToArray();
        }, ct).ConfigureAwait(false);
    }

    public async Task DeleteImageAsync(string nameOrId, CancellationToken ct = default)
    {
        var session = await GetSessionAsync(ct).ConfigureAwait(false);
        await Task.Run(() => session.DeleteImage(nameOrId), ct).ConfigureAwait(false);
    }

    public async Task TerminateSessionAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var session = _session;
            _session = null;
            if (session is null) return;
            await Task.Run(() => session.Terminate(), ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Session> GetSessionAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return _session ?? throw new InvalidOperationException("WSLC session is not started.");
        }
        finally
        {
            _gate.Release();
        }
    }
}
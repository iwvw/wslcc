using System.Text.RegularExpressions;

namespace WSLCC.Core.Cli;

public enum WslcFeature
{
    ImageDigests,
    PullAllTags,
    CopyFollowLink,
    Events,
}

public interface IWslcCapabilities
{
    Task<Version?> GetVersionAsync(CancellationToken ct = default);

    Task<bool> SupportsAsync(WslcFeature feature, CancellationToken ct = default);

    void Invalidate();
}

public sealed partial class WslcCapabilities : IWslcCapabilities
{
    private static readonly IReadOnlyDictionary<WslcFeature, Version> MinimumVersions =
        new Dictionary<WslcFeature, Version>
        {
            [WslcFeature.ImageDigests] = new(2, 9, 13),
            [WslcFeature.PullAllTags] = new(2, 9, 13),
            [WslcFeature.CopyFollowLink] = new(2, 9, 13),
            [WslcFeature.Events] = new(2, 9, 13),
        };

    private readonly WslcRunner _runner;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Version? _version;
    private bool _resolved;

    public WslcCapabilities(WslcRunner runner) => _runner = runner;

    public void Invalidate()
    {
        _gate.Wait();
        try
        {
            _resolved = false;
            _version = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Version?> GetVersionAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_resolved) return _version;
            _resolved = true;
            try
            {
                var output = await _runner.RunAsync(
                    ["--version"], new WslcRunner.RunOptions(CheckOutputForErrors: false), ct).ConfigureAwait(false);
                _version = ParseVersion(output);
            }
            catch
            {
                _version = null;
            }
            return _version;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> SupportsAsync(WslcFeature feature, CancellationToken ct = default)
    {
        if (!MinimumVersions.TryGetValue(feature, out var minimum)) return true;
        var version = await GetVersionAsync(ct).ConfigureAwait(false);
        return version is not null && version >= minimum;
    }

    private static Version? ParseVersion(string output)
    {
        var match = VersionPattern().Match(output);
        if (!match.Success) return null;
        var parts = new List<int>();
        foreach (var group in new[] { match.Groups[1], match.Groups[2], match.Groups[3], match.Groups[4] })
        {
            if (!group.Success) break;
            parts.Add(int.Parse(group.Value));
        }
        return parts.Count >= 3 ? new Version(string.Join('.', parts)) : null;
    }

    [GeneratedRegex(@"(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?")]
    private static partial Regex VersionPattern();
}

using System.Reflection;
using System.Text.Json;

namespace WSLCC.Core.Services;

public sealed record UpdateInfo(
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseUrl,
    string? Error,
    bool HasUpdate);

public interface IWslcUpdateService
{
    Task<UpdateInfo> CheckAsync(CancellationToken ct = default);
}

public sealed class WslcUpdateService : IWslcUpdateService
{
    private const string ReleasesApi = "https://api.github.com/repos/iwvw/wslcc/releases/latest";

    public async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        var current = GetCurrentVersion();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("WSLCC/" + current);
            var json = await http.GetStringAsync(ReleasesApi, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var url = root.TryGetProperty("html_url", out var u) ? u.GetString() : null;
            var latest = tag?.TrimStart('v');

            var hasUpdate = false;
            if (!string.IsNullOrEmpty(latest)
                && Version.TryParse(latest, out var latestV)
                && Version.TryParse(current, out var currentV))
                hasUpdate = latestV > currentV;

            return new UpdateInfo(current, latest, url, null, hasUpdate);
        }
        catch (Exception ex)
        {
            return new UpdateInfo(current, null, null, ex.Message, false);
        }
    }

    private static string GetCurrentVersion()
    {
        try
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
        catch
        {
            return "0.0.0";
        }
    }
}
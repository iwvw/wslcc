using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace WSLCC.Core.Services;

public sealed record UpdateInfo(
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseUrl,
    string? DownloadUrl,
    string? Error,
    bool HasUpdate);

public interface IWslcUpdateService
{
    Task<UpdateInfo> CheckAsync(CancellationToken ct = default);

    Task<UpdateInfo> DownloadAndInstallAsync(string? downloadUrl = null, IProgress<double>? progress = null, CancellationToken ct = default);
}

public sealed class WslcUpdateService : IWslcUpdateService
{
    private const string ReleasesApi = "https://api.github.com/repos/iwvw/wslcc/releases/latest";

    public static UpdateInfo? LastResult { get; private set; }

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

            var downloadUrl = default(string);
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (!asset.TryGetProperty("name", out var name)) continue;
                    if (!name.GetString()?.Contains("WSLCC-Setup", StringComparison.OrdinalIgnoreCase) ?? true) continue;
                    if (asset.TryGetProperty("browser_download_url", out var bdu))
                        downloadUrl = bdu.GetString();
                    break;
                }
            }

            var hasUpdate = false;
            if (!string.IsNullOrEmpty(latest)
                && Version.TryParse(latest, out var latestV)
                && Version.TryParse(current, out var currentV))
                hasUpdate = latestV > currentV;

            var result = new UpdateInfo(current, latest, url, downloadUrl, null, hasUpdate);
            LastResult = result;
            return result;
        }
        catch (Exception ex)
        {
            var result = new UpdateInfo(current, null, null, null, ex.Message, false);
            LastResult = result;
            return result;
        }
    }

    public async Task<UpdateInfo> DownloadAndInstallAsync(string? downloadUrl = null, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var info = LastResult ?? await CheckAsync(ct);
        if (info.Error is not null)
            return info with { Error = "无法获取更新信息：" + info.Error };

        if (string.IsNullOrEmpty(info.DownloadUrl) && string.IsNullOrEmpty(downloadUrl))
            return info with { Error = "发布中未找到安装包（WSLCC-Setup.exe）" };

        var url = !string.IsNullOrEmpty(downloadUrl) ? downloadUrl : info.DownloadUrl;
        var dest = Path.Combine(
            Path.GetTempPath(),
            $"WSLCC-Setup-{info.LatestVersion ?? "latest"}.exe");

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("WSLCC/" + info.CurrentVersion);
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? -1;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var target = File.Create(dest);
            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                readTotal += read;
                if (total > 0)
                    progress?.Report((double)readTotal / total);
            }

            var psi = new ProcessStartInfo
            {
                FileName = dest,
                Arguments = "/SILENT /SP- /NORESTART",
                UseShellExecute = true,
                WorkingDirectory = Path.GetTempPath(),
            };
            Process.Start(psi);

            return info with { Error = null };
        }
        catch (Exception ex)
        {
            return info with { Error = "下载或启动安装失败：" + ex.Message };
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
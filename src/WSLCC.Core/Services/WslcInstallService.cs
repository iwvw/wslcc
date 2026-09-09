using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using WSLCC.Core.Cli;

namespace WSLCC.Core.Services;

public sealed record WslcInstallInfo(
    string? CurrentWslcVersion,
    string? LatestWslVersion,
    string? Error)
{
    public bool WslcInstalled => !string.IsNullOrEmpty(CurrentWslcVersion);

    public bool HasUpdate
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentWslcVersion) || string.IsNullOrEmpty(LatestWslVersion)) return false;
            var cur = Normalize(CurrentWslcVersion);
            var latest = Normalize(LatestWslVersion);
            return Version.TryParse(cur, out var cv) && Version.TryParse(latest, out var lv) && lv > cv;
        }
    }

    private static string Normalize(string v)
    {
        v = v.Trim();
        if (v.StartsWith('v')) v = v[1..];
        return v;
    }
}

public interface IWslcInstallService
{
    Task<WslcInstallInfo> GetStatusAsync(CancellationToken ct = default);

    Task<string> RunInstallOrUpgradeAsync();
}

public sealed class WslcInstallService : IWslcInstallService
{
    private const string LatestWslReleasesApi = "https://api.github.com/repos/microsoft/WSL/releases/latest";

    private readonly WslcRunner _runner;

    public WslcInstallService(WslcRunner runner) => _runner = runner;

    public async Task<WslcInstallInfo> GetStatusAsync(CancellationToken ct = default)
    {
        string? current = null;
        try
        {
            var json = await _runner.RunAsync(["info", "--format", "json"], ct: ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Client", out var client)
                && client.TryGetProperty("Version", out var ver))
                current = ver.ValueKind == JsonValueKind.String ? ver.GetString() : null;
        }
        catch
        {
        }

        string? latest = null;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("WSLCC/0.1.0");
            var json = await http.GetStringAsync(LatestWslReleasesApi, ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("tag_name", out var tag))
                latest = tag.GetString();
        }
        catch (Exception ex)
        {
            return new WslcInstallInfo(current, latest, ex.Message);
        }

        return new WslcInstallInfo(current, latest, null);
    }

    public Task<string> RunInstallOrUpgradeAsync()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "winget.exe",
            Arguments = "install --id Microsoft.Windows.WSL --accept-package-agreements --accept-source-agreements",
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Environment.SystemDirectory,
        };
        try
        {
            using var process = Process.Start(psi);
            return Task.FromResult(
                process is null
                    ? "未能启动 winget，请手动打开 PowerShell（管理员）执行：winget install --id Microsoft.Windows.WSL"
                    : "已弹窗请求管理员权限，winget 将安装/更新 WSL（含 wslc）。请在弹出的窗口中选择“是”。");
        }
        catch (Win32Exception)
        {
            return Task.FromResult("管理员权限被拒绝或 winget 不可用，可手动执行：winget install --id Microsoft.Windows.WSL");
        }
    }
}
using System.Text.Json;
using Microsoft.WSL.Containers;
using WSLCC.Core.Cli;
using WSLCC.Core.Models;

namespace WSLCC.Core.Services;

public interface IWslcEnvironmentService
{
    Task<WslcEnvironmentInfo> GetEnvironmentAsync(CancellationToken ct = default);
}

public sealed class WslcEnvironmentService : IWslcEnvironmentService
{
    private readonly WslcRunner _runner;

    public WslcEnvironmentService(WslcRunner runner) => _runner = runner;

    public async Task<WslcEnvironmentInfo> GetEnvironmentAsync(CancellationToken ct = default)
    {
        var client = default(WslcClientInfo);
        var server = default(WslcServerInfo);
        try
        {
            var json = await _runner.RunAsync(["info", "--format", "json"], ct: ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("Client", out var clientEl))
            {
                client = new WslcClientInfo(
                    JsonGet(clientEl, "Version") ?? "",
                    JsonGet(clientEl, "WindowsVersion") ?? "",
                    JsonGet(clientEl, "KernelVersion") ?? "",
                    JsonGet(clientEl, "SettingsFile") ?? "");
            }
            if (root.TryGetProperty("Server", out var serverEl))
            {
                var sessions = new List<WslcSessionInfo>();
                if (serverEl.TryGetProperty("Sessions", out var sessionsEl))
                {
                    foreach (var s in sessionsEl.EnumerateArray())
                    {
                        sessions.Add(new WslcSessionInfo(
                            s.TryGetProperty("ID", out var id) && id.TryGetUInt32(out var idv) ? idv : 0,
                            JsonGet(s, "Name") ?? "",
                            s.TryGetProperty("CreatorPid", out var pid) && pid.TryGetUInt32(out var pidv) ? pidv : null));
                    }
                }
                server = new WslcServerInfo(JsonGet(serverEl, "SessionManagerVersion") ?? "", sessions);
            }
        }
        catch (WslcCliException)
        {
        }
        catch (JsonException)
        {
        }

        var missing = Array.Empty<string>();
        var apiAvailable = false;
        try
        {
            var components = WslcService.GetMissingComponents();
            apiAvailable = true;
            missing = components.Select(c => c.ToString()).ToArray();
        }
        catch
        {
        }

        return new WslcEnvironmentInfo(client, server, missing, apiAvailable);
    }

    private static string? JsonGet(JsonElement element, string name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

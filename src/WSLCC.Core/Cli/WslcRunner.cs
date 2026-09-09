using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WSLCC.Core.Cli;

public sealed partial class WslcRunner
{
    public sealed record RunOptions(bool ThrowOnWslcError = true, bool StreamOutput = false);

    public async Task<string> RunAsync(string arguments, RunOptions? options = null, CancellationToken ct = default)
    {
        var psi = CreateProcess(arguments);
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start wslc.exe.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        var combined = (stderr + "\n" + stdout).Trim();

        if (options?.ThrowOnWslcError != false && IsWslcError(combined))
            throw WslcCliException.FromOutput(combined, ExtractErrorCode(combined));
        return stdout;
    }

    public async Task RunStreamingAsync(string arguments, IProgress<string>? progress, CancellationToken ct = default)
    {
        var psi = CreateProcess(arguments);
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start wslc.exe.");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stdout.AppendLine(e.Data);
            progress?.Report(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stderr.AppendLine(e.Data);
            progress?.Report(e.Data);
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        var combined = string.Concat(stderr, stdout);
        if (IsWslcError(combined))
            throw WslcCliException.FromOutput(combined, ExtractErrorCode(combined));
    }

    public async Task<IReadOnlyList<JsonElement>> RunJsonLinesAsync(string arguments, CancellationToken ct = default)
    {
        var output = await RunAsync(arguments, ct: ct).ConfigureAwait(false);
        return ParseJsonLines(output);
    }

    private static ProcessStartInfo CreateProcess(string arguments)
    {
        return new ProcessStartInfo("wslc.exe", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
    }

    private static IReadOnlyList<JsonElement> ParseJsonLines(string output)
    {
        var results = new List<JsonElement>();
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            if (trimmed[0] != '{') continue;
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                results.Add(doc.RootElement.Clone());
            }
            catch (JsonException)
            {
            }
        }
        return results;
    }

    private static bool IsWslcError(string combined)
        => combined.Contains("灾难性故障", StringComparison.Ordinal)
        || combined.Contains("Catastrophic", StringComparison.OrdinalIgnoreCase)
        || combined.Contains("错误代码", StringComparison.Ordinal)
        || combined.Contains("Error code", StringComparison.OrdinalIgnoreCase)
        || combined.Contains("error:", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractErrorCode(string combined)
    {
        var match = ErrorCodePattern().Match(combined);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"(?:错误代码|Error code)\s*[：:]\s*([A-Za-z0-9_]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ErrorCodePattern();
}
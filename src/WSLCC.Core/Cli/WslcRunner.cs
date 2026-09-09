using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WSLCC.Core.Cli;

public sealed partial class WslcRunner
{
    public sealed record RunOptions(bool ThrowOnWslcError = true, bool CheckOutputForErrors = true);

    public async Task<string> RunAsync(IReadOnlyList<string> args, RunOptions? options = null, CancellationToken ct = default)
    {
        using var process = StartProcess(args);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            throw;
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        var combined = (stderr + "\n" + stdout).Trim();

        if (options?.CheckOutputForErrors != false && IsWslcError(combined))
        {
            if (options?.ThrowOnWslcError == false) return stdout;
            throw WslcCliException.FromOutput(combined, ExtractErrorCode(combined));
        }
        return stdout;
    }

    public async Task RunStreamingAsync(
        IReadOnlyList<string> args, RunOptions? options, IProgress<string>? progress, CancellationToken ct = default)
    {
        using var process = StartProcess(args);

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
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            throw;
        }

        process.WaitForExit();

        var combined = string.Concat(stderr, stdout);
        if (options?.CheckOutputForErrors != false && IsWslcError(combined))
            throw WslcCliException.FromOutput(combined, ExtractErrorCode(combined));
    }

    public async Task<IReadOnlyList<JsonElement>> RunJsonLinesAsync(IReadOnlyList<string> args, CancellationToken ct = default)
    {
        var output = await RunAsync(args, ct: ct).ConfigureAwait(false);
        return ParseJsonLines(output);
    }

    private static Process StartProcess(IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo("wslc.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        return Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 wslc.exe。");
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    private static IReadOnlyList<JsonElement> ParseJsonLines(string output)
    {
        var results = new List<JsonElement>();
        var failed = 0;
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            if (trimmed[0] != '{') { failed++; continue; }
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                results.Add(doc.RootElement.Clone());
            }
            catch (JsonException)
            {
                failed++;
            }
        }
        if (results.Count == 0 && failed > 0)
            throw new WslcCliException(
                $"wslc 输出无法解析为 JSON（{failed} 行非 JSON）：{Truncate(output)}");
        return results;
    }

    private static string Truncate(string text, int max = 300)
    {
        text = text.Trim();
        return text.Length <= max ? text : text[..max] + "…";
    }

    private static bool IsWslcError(string combined)
    {
        if (string.IsNullOrWhiteSpace(combined)) return false;
        if (combined.Contains("灾难性故障", StringComparison.Ordinal)) return true;
        if (combined.Contains("Catastrophic", StringComparison.OrdinalIgnoreCase)) return true;
        if (combined.Contains("错误代码", StringComparison.Ordinal)) return true;
        if (combined.Contains("Error code", StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var line in combined.Split('\n'))
        {
            if (line.TrimStart().StartsWith("error:", StringComparison.OrdinalIgnoreCase)) return true;
        }
        if (HresultPattern().IsMatch(combined)) return true;
        return false;
    }

    private static string? ExtractErrorCode(string combined)
    {
        var match = ErrorCodePattern().Match(combined);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"(?:错误代码|Error code)\s*[：:]\s*([A-Za-z0-9_]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ErrorCodePattern();

    [GeneratedRegex(@"\bE_[A-Z0-9]+\b", RegexOptions.IgnoreCase)]
    private static partial Regex HresultPattern();
}
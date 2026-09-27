using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WSLCC.Core.Cli;

public sealed partial class WslcRunner
{
    public sealed record RunOptions(bool ThrowOnWslcError = true, bool CheckOutputForErrors = true);

    private string? _session;

    private static string? _executablePath;

    public string? Session
    {
        get => Volatile.Read(ref _session);
        set => Volatile.Write(ref _session, string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

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

    private Process StartProcess(IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo(ResolveExecutable())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        var session = Session;
        if (session is not null)
        {
            psi.ArgumentList.Add("--session");
            psi.ArgumentList.Add(session);
        }

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        try
        {
            return Process.Start(psi)
                ?? throw new WslcCliException("无法启动 wslc.exe。");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            throw new WslcCliException(
                "未找到 wslc.exe。请先安装 WSL 容器 CLI 并确保其位于 PATH 中，"
                + "或在管理员终端执行 wsl --update --pre-release 后重启应用。"
                + $"（搜索路径：{psi.FileName}）");
        }
    }

    private static string ResolveExecutable()
    {
        var cached = Volatile.Read(ref _executablePath);
        if (cached is not null) return cached;

        var resolved = FindOnPath("wslc.exe") ?? FindInKnownLocations() ?? "wslc.exe";
        Volatile.Write(ref _executablePath, resolved);
        return resolved;
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim('"'), fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch
            {
            }
        }
        return null;
    }

    private static string? FindInKnownLocations()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            Path.Combine(programFiles, "WSL", "wslc.exe"),
            Path.Combine(localAppData, "Microsoft", "WindowsApps", "wslc.exe"),
        };
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }
        return null;
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
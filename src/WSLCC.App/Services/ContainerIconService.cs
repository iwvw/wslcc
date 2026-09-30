using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WSLCC.App.Services;

public sealed partial class ContainerIconService
{
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WSLCC", "icons");

    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(7);

    private static readonly string[] Candidates =
    {
        "/favicon.ico",
        "/favicon.svg",
        "/favicon.png",
        "/apple-touch-icon.png",
        "/apple-touch-icon-precomposed.png",
        "/logo.png",
    };

    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, string?> _memory = new(StringComparer.OrdinalIgnoreCase);

    public static ContainerIconService Shared { get; } = new();

    public ContainerIconService()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("WSLCC/1.5");
        try
        {
            Directory.CreateDirectory(CacheDirectory);
        }
        catch
        {
        }
    }

    public string? TryGetCached(string webUrl)
    {
        if (string.IsNullOrWhiteSpace(webUrl)) return null;
        if (_memory.TryGetValue(webUrl, out var cached)) return cached;
        var path = CachePathFor(webUrl);
        if (File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < CacheTtl)
        {
            _memory[webUrl] = path;
            return path;
        }
        return null;
    }

    public async Task<string?> ResolveAsync(string webUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(webUrl)) return null;

        var existing = TryGetCached(webUrl);
        if (existing is not null) return existing;

        if (!Uri.TryCreate(webUrl, UriKind.Absolute, out var baseUri)) return null;

        var ext = await TryDiscoverDeclaredIconAsync(baseUri, ct).ConfigureAwait(false);
        if (ext is not null && TryDecodeDataUri(ext, out var inlineBytes))
            return WriteCache(webUrl, inlineBytes);

        var candidates = new List<string>();
        if (ext is not null && !ext.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) candidates.Add(ext);
        candidates.AddRange(Candidates);

        foreach (var candidate in candidates)
        {
            try
            {
                var uri = new Uri(baseUri, candidate);
                using var response = await _http
                    .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) continue;

                var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                if (bytes.Length == 0 || bytes.Length > 512 * 1024) continue;
                if (!LooksLikeImage(bytes)) continue;

                return WriteCache(webUrl, bytes);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }

        _memory[webUrl] = null;
        return null;
    }

    private string? WriteCache(string webUrl, byte[] bytes)
    {
        try
        {
            var normalized = NormalizeSvg(bytes);
            var path = CachePathFor(webUrl);
            File.WriteAllBytes(path, normalized ?? bytes);
            _memory[webUrl] = path;
            return path;
        }
        catch
        {
            _memory[webUrl] = null;
            return null;
        }
    }

    private static byte[]? NormalizeSvg(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        if (!text.Contains("<svg", StringComparison.OrdinalIgnoreCase)) return null;
        if (!text.Contains("<style", StringComparison.OrdinalIgnoreCase)) return null;

        var rules = ParseCssRules(text);
        if (rules.Count == 0) return null;

        foreach (Match element in ElementPattern().Matches(text))
        {
            var tag = element.Value;
            var classMatch = ClassAttributePattern().Match(tag);
            if (!classMatch.Success) continue;

            var declarations = new StringBuilder();
            foreach (var cls in classMatch.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (rules.TryGetValue("." + cls, out var decls)) declarations.Append(decls);
            }
            if (declarations.Length == 0) continue;

            var styleMatch = StyleAttributePattern().Match(tag);
            var merged = styleMatch.Success
                ? $"{styleMatch.Groups[1].Value.TrimEnd(';')};{declarations}"
                : declarations.ToString().TrimEnd(';');

            string replaced;
            if (styleMatch.Success)
                replaced = tag[..styleMatch.Index] + $"style=\"{merged}\"" + tag[(styleMatch.Index + styleMatch.Length)..];
            else
            {
                var close = tag.LastIndexOf('>');
                var insertAt = close > 0 && tag[close - 1] == '/' ? close - 1 : close;
                replaced = tag[..insertAt] + $" style=\"{merged}\"" + tag[insertAt..];
            }

            text = text.Replace(tag, replaced);
        }

        return Encoding.UTF8.GetBytes(text);
    }

    private static Dictionary<string, string> ParseCssRules(string svg)
    {
        var rules = new Dictionary<string, string>(StringComparer.Ordinal);
        var styleBlock = StyleBlockPattern().Match(svg);
        if (!styleBlock.Success) return rules;

        foreach (Match rule in CssRulePattern().Matches(styleBlock.Groups[1].Value))
        {
            var selectors = rule.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var body = rule.Groups[2].Value;
            foreach (var selector in selectors)
            {
                var key = selector.Trim();
                if (key.StartsWith('.') || key.StartsWith('#'))
                {
                    rules[key] = rules.TryGetValue(key, out var existing) ? existing + body : body;
                }
            }
        }
        return rules;
    }

    private static bool TryDecodeDataUri(string value, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        const string prefix = "data:";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var comma = value.IndexOf(',');
        if (comma < 0) return false;

        var meta = value[prefix.Length..comma];
        var payload = value[(comma + 1)..];
        try
        {
            if (meta.Contains("base64", StringComparison.OrdinalIgnoreCase))
            {
                bytes = Convert.FromBase64String(payload.Trim());
            }
            else
            {
                bytes = Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
            }
            return bytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string?> TryDiscoverDeclaredIconAsync(Uri baseUri, CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(baseUri, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            if (response.Content.Headers.ContentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) != true)
                return null;

            var html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (html.Length > 256 * 1024) html = html[..(256 * 1024)];

            string? best = null;
            var bestScore = -1;
            foreach (Match link in LinkTagPattern().Matches(html))
            {
                var tag = link.Value;
                var rel = AttributePattern("rel").Match(tag).Groups[1].Value;
                var href = AttributePattern("href").Match(tag).Groups[1].Value.Trim();
                if (href.Length == 0) continue;
                if (!rel.Contains("icon", StringComparison.OrdinalIgnoreCase)) continue;
                var score = rel.Contains("apple-touch", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = href;
                }
            }
            return best;
        }
        catch
        {
            return null;
        }
    }

    private static bool LooksLikeImage(byte[] bytes)
    {
        if (bytes.Length < 4) return false;
        if (bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0x01 && bytes[3] == 0x00) return true;
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true;
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return true;
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return true;
        if (bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46) return true;
        return Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 512))
            .Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    private static string CachePathFor(string webUrl)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(webUrl)))[..16];
        return Path.Combine(CacheDirectory, $"{hash}.icon");
    }

    [GeneratedRegex("<link\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LinkTagPattern();

    [GeneratedRegex("<style\\b[^>]*>(.*?)</style>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleBlockPattern();

    [GeneratedRegex("([^{}]+)\\{([^{}]*)\\}", RegexOptions.Singleline)]
    private static partial Regex CssRulePattern();

    [GeneratedRegex("<[a-zA-Z][^>]*>", RegexOptions.Singleline)]
    private static partial Regex ElementPattern();

    [GeneratedRegex("class\\s*=\\s*[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex ClassAttributePattern();

    [GeneratedRegex("style\\s*=\\s*[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex StyleAttributePattern();

    private static Regex AttributePattern(string name)
        => new($"{name}\\s*=\\s*[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase);
}

using System.Text;
using YamlDotNet.RepresentationModel;

namespace WSLCC.Core.Services;

public sealed record WslcSessionFileSettings(
    string? CpuCount,
    string? MemorySize,
    string? MaxStorageSize,
    string? StoragePath,
    string? DefaultBindingAddress,
    string? HostLoopback,
    string? IdleTimeout,
    string? CredentialStore)
{
    public static WslcSessionFileSettings Empty { get; } = new(null, null, null, null, null, null, null, null);
}

public interface IWslcSettingsFileService
{
    string FilePath { get; }

    WslcSessionFileSettings Read();

    void Write(WslcSessionFileSettings settings);
}

public sealed class WslcSettingsFileService : IWslcSettingsFileService
{
    public const string DefaultSentinel = "default";

    private static readonly string[] SessionKeys =
    [
        "cpuCount",
        "memorySize",
        "maxStorageSize",
        "storagePath",
        "defaultBindingAddress",
        "hostLoopback",
        "idleTimeout",
    ];

    public string FilePath { get; }

    public WslcSettingsFileService(string? filePath = null)
        => FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "wslc",
            "settings.yaml");

    public WslcSessionFileSettings Read()
    {
        if (!File.Exists(FilePath)) return WslcSessionFileSettings.Empty;
        try
        {
            var yaml = new YamlStream();
            using var reader = new StreamReader(FilePath);
            yaml.Load(reader);
            if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                return WslcSessionFileSettings.Empty;

            var session = GetMap(root, "session");
            return new WslcSessionFileSettings(
                session is null ? null : GetScalar(session, "cpuCount"),
                session is null ? null : GetScalar(session, "memorySize"),
                session is null ? null : GetScalar(session, "maxStorageSize"),
                session is null ? null : GetScalar(session, "storagePath"),
                session is null ? null : GetScalar(session, "defaultBindingAddress"),
                session is null ? null : GetScalar(session, "hostLoopback"),
                session is null ? null : GetScalar(session, "idleTimeout"),
                GetScalar(root, "credentialStore"));
        }
        catch
        {
            return WslcSessionFileSettings.Empty;
        }
    }

    public void Write(WslcSessionFileSettings settings)
    {
        var lines = File.Exists(FilePath)
            ? new List<string>(File.ReadAllLines(FilePath))
            : new List<string>();

        if (lines.Count == 0)
        {
            lines.Add("# wslc user settings");
            lines.Add("# https://aka.ms/wslc-settings");
            lines.Add("# All settings support string value \"default\" which uses built-in defaults.");
            lines.Add(string.Empty);
            lines.Add("session:");
        }

        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["cpuCount"] = settings.CpuCount,
            ["memorySize"] = settings.MemorySize,
            ["maxStorageSize"] = settings.MaxStorageSize,
            ["storagePath"] = settings.StoragePath,
            ["defaultBindingAddress"] = settings.DefaultBindingAddress,
            ["hostLoopback"] = settings.HostLoopback,
            ["idleTimeout"] = settings.IdleTimeout,
        };

        foreach (var key in SessionKeys)
            ApplySessionKey(lines, key, values[key]);

        ApplyTopLevelKey(lines, "credentialStore", settings.CredentialStore);

        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllLines(FilePath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void ApplySessionKey(List<string> lines, string key, string? value)
    {
        var sessionIndex = FindSectionHeader(lines, "session", 0);
        if (sessionIndex < 0)
        {
            lines.Add(string.Empty);
            lines.Add("session:");
            sessionIndex = lines.Count - 1;
        }

        var end = sessionIndex + 1;
        while (end < lines.Count)
        {
            var line = lines[end];
            if (line.Trim().Length == 0) { end++; continue; }
            if (CountIndent(line) == 0) break;
            end++;
        }

        var indent = DetectChildIndent(lines, sessionIndex, end);
        var activeIndex = FindKey(lines, sessionIndex + 1, end, key, indent, commented: false);
        var commentedIndex = activeIndex >= 0 ? -1 : FindKey(lines, sessionIndex + 1, end, key, indent, commented: true);
        var target = activeIndex >= 0 ? activeIndex : commentedIndex;

        if (value is null)
        {
            if (activeIndex >= 0)
                lines[activeIndex] = $"{indent}# {key}: {DefaultSentinel}";
            return;
        }

        var formatted = $"{indent}{key}: {value}";
        if (target >= 0) lines[target] = formatted;
        else lines.Insert(end, formatted);
    }

    private static void ApplyTopLevelKey(List<string> lines, string key, string? value)
    {
        var activeIndex = FindKey(lines, 0, lines.Count, key, string.Empty, commented: false);
        var commentedIndex = activeIndex >= 0 ? -1 : FindKey(lines, 0, lines.Count, key, string.Empty, commented: true);

        if (value is null)
        {
            if (activeIndex >= 0)
                lines[activeIndex] = $"# {key}: {DefaultSentinel}";
            return;
        }

        var formatted = $"{key}: {value}";
        if (activeIndex >= 0) lines[activeIndex] = formatted;
        else if (commentedIndex >= 0) lines[commentedIndex] = formatted;
        else lines.Add(formatted);
    }

    private static int FindSectionHeader(List<string> lines, string name, int indent)
    {
        var prefix = new string(' ', indent) + name + ":";
        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimEnd();
            if (trimmed.Equals(prefix, StringComparison.Ordinal)) return i;
        }
        return -1;
    }

    private static int FindKey(
        List<string> lines, int start, int end, string key, string indent, bool commented)
    {
        var active = indent + key + ":";
        var comment = indent + "# " + key + ":";
        for (var i = start; i < end && i < lines.Count; i++)
        {
            var line = lines[i];
            if (commented)
            {
                if (line.StartsWith(comment, StringComparison.Ordinal)) return i;
                if (line.StartsWith(indent + "#" + key + ":", StringComparison.Ordinal)) return i;
            }
            else if (line.StartsWith(active, StringComparison.Ordinal))
            {
                var after = line[(indent.Length + key.Length + 1)..];
                if (after.Length == 0 || after[0] is ' ' or '\t' or ':') return i;
            }
        }
        return -1;
    }

    private static string DetectChildIndent(List<string> lines, int headerIndex, int end)
    {
        for (var i = headerIndex + 1; i < end && i < lines.Count; i++)
        {
            if (lines[i].Trim().Length == 0) continue;
            var indent = CountIndent(lines[i]);
            if (indent > 0) return new string(' ', indent);
        }
        return "  ";
    }

    private static int CountIndent(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ') count++;
        return count;
    }

    private static YamlMappingNode? GetMap(YamlMappingNode map, string key)
        => map.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlMappingNode child
            ? child
            : null;

    private static string? GetScalar(YamlMappingNode map, string key)
    {
        if (!map.Children.TryGetValue(new YamlScalarNode(key), out var node)) return null;
        return node is YamlScalarNode scalar ? scalar.Value : null;
    }
}

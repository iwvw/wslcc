namespace WSLCC.Core.Services;

public static class WslcPathDiagnostics
{
    public static IReadOnlyList<string> DetectUntrustedReparsePoints(params string?[] paths)
    {
        var result = new List<string>();
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            try
            {
                var full = Environment.ExpandEnvironmentVariables(path);
                if (Directory.Exists(full))
                {
                    var info = new DirectoryInfo(full);
                    if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        result.Add(full);
                }
                else if (File.Exists(full))
                {
                    var info = new FileInfo(full);
                    if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        result.Add(full);
                }
            }
            catch
            {
            }
        }
        return result;
    }

    public static string SettingsDirectory
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "wslc");
}

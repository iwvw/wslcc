namespace WSLCC.Data;

public static class WslcAppData
{
    public static string ResolveDirectory()
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath)
            ?? Path.GetDirectoryName(AppContext.BaseDirectory)
            ?? Directory.GetCurrentDirectory();

        var local = Path.Combine(exeDir, "data");
        if (CanWrite(local))
            return local;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WSLCC");
    }

    private static bool CanWrite(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, ".probe");
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string LegacyComposeDirectory()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WSLCC", "compose");
}
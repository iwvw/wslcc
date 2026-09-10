using System.Globalization;

namespace WSLCC_App;

public static class L
{
    private static readonly IReadOnlyDictionary<string, string> Table =
        CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? AppStrings.Zh
            : AppStrings.En;

    public static string Get(string key)
    {
        return Table.TryGetValue(key, out var value) && value.Length > 0 ? value : key;
    }

    public static string GetFormat(string key, params object?[] args)
    {
        try
        {
            return string.Format(Get(key), args ?? []);
        }
        catch
        {
            return key;
        }
    }
}
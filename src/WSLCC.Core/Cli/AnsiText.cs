using System.Text.RegularExpressions;

namespace WSLCC.Core.Cli;

public static partial class AnsiText
{
    public static string Strip(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        var cleaned = AnsiEscapePattern().Replace(text, string.Empty);
        cleaned = ControlPattern().Replace(cleaned, string.Empty);
        return cleaned;
    }

    [GeneratedRegex(@"\x1B(?:\[[0-9;?]*[ -/]*[@-~]|\][^\x07\x1B]*(?:\x07|\x1B\\))")]
    private static partial Regex AnsiEscapePattern();

    [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F]")]
    private static partial Regex ControlPattern();
}
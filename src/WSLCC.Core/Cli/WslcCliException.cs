namespace WSLCC.Core.Cli;

public sealed class WslcCliException : Exception
{
    public string? WslcErrorCode { get; }

    public WslcCliException(string message, string? wslcErrorCode = null)
        : base(string.IsNullOrEmpty(wslcErrorCode) ? message : $"{message} ({wslcErrorCode})")
    {
        WslcErrorCode = wslcErrorCode;
    }

    public static WslcCliException FromOutput(string combined, string? errorCode = null)
        => new(combined.Trim(), errorCode);
}
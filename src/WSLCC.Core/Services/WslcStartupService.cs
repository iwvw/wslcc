using System.Diagnostics;
using Microsoft.Win32;

namespace WSLCC.Core.Services;

public interface IWslcStartupService
{
    bool IsEnabled();

    bool SetEnabled(bool enabled);
}

public sealed class WslcStartupService : IWslcStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WSLCC";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            return false;
        }
    }

    public bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled)
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exe))
                    exe = Path.Combine(AppContext.BaseDirectory, "WSLCC.exe");
                key?.SetValue(ValueName, $"\"{exe}\"");
            }
            else
            {
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
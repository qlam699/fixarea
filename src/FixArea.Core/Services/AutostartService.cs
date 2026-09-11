using Microsoft.Win32;

namespace FixArea.Core.Services;

public static class AutostartService
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "FixArea";

    public static void Apply(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{executablePath}\"");
        else if (key.GetValue(ValueName) is not null)
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

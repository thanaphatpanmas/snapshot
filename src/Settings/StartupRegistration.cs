using Microsoft.Win32;
using System.Windows.Forms;

// The registry is the single source of truth for "run at startup"; it is deliberately not duplicated in the settings file.
static class StartupRegistration
{
    const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled
    {
        get
        {
            using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath))
                return runKey != null && runKey.GetValue(AppInfo.Name) != null;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using (RegistryKey runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath))
        {
            if (enabled) runKey.SetValue(AppInfo.Name, "\"" + Application.ExecutablePath + "\"");
            else runKey.DeleteValue(AppInfo.Name, false);
        }
    }
}

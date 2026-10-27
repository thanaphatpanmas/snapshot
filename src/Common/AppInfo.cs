using System;
using System.IO;

static class AppInfo
{
    public const string Name = "Snapshot";

    public static string SettingsFolder
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Name); }
    }

    public static string SettingsFilePath
    {
        get { return Path.Combine(SettingsFolder, "settings.ini"); }
    }
}

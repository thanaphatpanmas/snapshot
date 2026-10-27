using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

enum CaptureAction
{
    Region,
    FullScreen,
    Window,
    RepeatLastRegion,
    Ocr,
    Record
}

enum ImageFileFormat
{
    Png,
    Jpg,
    Bmp
}

enum RecordingFormat
{
    Gif,
    Mp4
}

class AppSettings
{
    public static readonly CaptureAction[] AllActions = (CaptureAction[])Enum.GetValues(typeof(CaptureAction));

    Dictionary<CaptureAction, Hotkey> hotkeys = new Dictionary<CaptureAction, Hotkey>();

    public string SaveFolder { get; set; }
    public string FileNameTemplate { get; set; }
    public ImageFileFormat Format { get; set; }
    public int JpegQuality { get; set; }
    public bool CopyToClipboard { get; set; }
    public bool PlayShutterSound { get; set; }
    public bool IncludeCursor { get; set; }
    public bool ShowMagnifier { get; set; }
    public int DelaySeconds { get; set; }
    public string Language { get; set; }
    public RecordingFormat RecordFormat { get; set; }
    public int RecordFramesPerSecond { get; set; }
    public int RecordMaxSeconds { get; set; }

    public static AppSettings CreateDefault()
    {
        var settings = new AppSettings
        {
            SaveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots"),
            FileNameTemplate = "Screenshot {date} at {time}",
            Format = ImageFileFormat.Png,
            JpegQuality = 90,
            PlayShutterSound = true,
            Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "th" ? "th" : "en",
            RecordFormat = RecordingFormat.Gif,
            RecordFramesPerSecond = 10,
            RecordMaxSeconds = 60
        };
        settings.ResetHotkeys();
        return settings;
    }

    public Hotkey GetHotkey(CaptureAction action)
    {
        return hotkeys[action];
    }

    public void SetHotkey(CaptureAction action, Hotkey hotkey)
    {
        hotkeys[action] = hotkey;
    }

    public void ResetHotkeys()
    {
        foreach (CaptureAction action in AllActions)
            hotkeys[action] = DefaultHotkey(action);
    }

    public AppSettings Clone()
    {
        var copy = (AppSettings)MemberwiseClone();
        copy.hotkeys = new Dictionary<CaptureAction, Hotkey>(hotkeys);
        return copy;
    }

    static Hotkey DefaultHotkey(CaptureAction action)
    {
        const HotkeyModifiers ctrlShift = HotkeyModifiers.Control | HotkeyModifiers.Shift;
        switch (action)
        {
            case CaptureAction.Region: return new Hotkey(ctrlShift, Keys.D4);
            case CaptureAction.FullScreen: return new Hotkey(ctrlShift, Keys.D3);
            case CaptureAction.Window: return new Hotkey(ctrlShift, Keys.D2);
            case CaptureAction.RepeatLastRegion: return new Hotkey(ctrlShift, Keys.D1);
            case CaptureAction.Record: return new Hotkey(ctrlShift, Keys.D5);
            default: return new Hotkey(ctrlShift, Keys.D6);
        }
    }
}

// Plain "Key=Value" lines: unknown or invalid entries fall back to defaults so a hand-edited file can't break startup.
static class SettingsStore
{
    const string HotkeyPrefix = "Hotkey.";

    public static AppSettings Load()
    {
        return Load(AppInfo.SettingsFilePath);
    }

    public static AppSettings Load(string filePath)
    {
        AppSettings settings = AppSettings.CreateDefault();
        if (!File.Exists(filePath)) return settings;

        foreach (string line in File.ReadAllLines(filePath))
        {
            int separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0) continue;
            Apply(settings, line.Substring(0, separatorIndex).Trim(), line.Substring(separatorIndex + 1).Trim());
        }
        return settings;
    }

    public static void Save(AppSettings settings)
    {
        Save(settings, AppInfo.SettingsFilePath);
    }

    public static void Save(AppSettings settings, string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        var lines = new List<string>
        {
            "SaveFolder=" + settings.SaveFolder,
            "FileNameTemplate=" + settings.FileNameTemplate,
            "Format=" + settings.Format,
            "JpegQuality=" + settings.JpegQuality,
            "CopyToClipboard=" + settings.CopyToClipboard,
            "PlayShutterSound=" + settings.PlayShutterSound,
            "IncludeCursor=" + settings.IncludeCursor,
            "ShowMagnifier=" + settings.ShowMagnifier,
            "DelaySeconds=" + settings.DelaySeconds,
            "Language=" + settings.Language,
            "RecordFormat=" + settings.RecordFormat,
            "RecordFramesPerSecond=" + settings.RecordFramesPerSecond,
            "RecordMaxSeconds=" + settings.RecordMaxSeconds
        };
        foreach (CaptureAction action in AppSettings.AllActions)
            lines.Add(HotkeyPrefix + action + "=" + settings.GetHotkey(action));
        File.WriteAllLines(filePath, lines.ToArray());
    }

    static void Apply(AppSettings settings, string key, string value)
    {
        if (key.StartsWith(HotkeyPrefix))
        {
            ApplyHotkey(settings, key.Substring(HotkeyPrefix.Length), value);
            return;
        }

        bool flag;
        int number;
        switch (key)
        {
            case "SaveFolder": if (value.Length > 0) settings.SaveFolder = value; break;
            case "FileNameTemplate": if (value.Length > 0) settings.FileNameTemplate = value; break;
            case "Format": settings.Format = ParseEnum(value, settings.Format); break;
            case "JpegQuality": if (int.TryParse(value, out number)) settings.JpegQuality = Clamp(number, 1, 100); break;
            case "CopyToClipboard": if (bool.TryParse(value, out flag)) settings.CopyToClipboard = flag; break;
            case "PlayShutterSound": if (bool.TryParse(value, out flag)) settings.PlayShutterSound = flag; break;
            case "IncludeCursor": if (bool.TryParse(value, out flag)) settings.IncludeCursor = flag; break;
            case "ShowMagnifier": if (bool.TryParse(value, out flag)) settings.ShowMagnifier = flag; break;
            case "DelaySeconds": if (int.TryParse(value, out number)) settings.DelaySeconds = Clamp(number, 0, 60); break;
            case "Language": if (value == "en" || value == "th") settings.Language = value; break;
            case "RecordFormat": settings.RecordFormat = ParseEnum(value, settings.RecordFormat); break;
            case "RecordFramesPerSecond": if (int.TryParse(value, out number)) settings.RecordFramesPerSecond = Clamp(number, 1, 30); break;
            case "RecordMaxSeconds": if (int.TryParse(value, out number)) settings.RecordMaxSeconds = Clamp(number, 1, 600); break;
        }
    }

    static void ApplyHotkey(AppSettings settings, string actionName, string value)
    {
        CaptureAction action;
        Hotkey hotkey = Hotkey.Parse(value);
        if (hotkey != null && Enum.TryParse(actionName, out action))
            settings.SetHotkey(action, hotkey);
    }

    static T ParseEnum<T>(string value, T fallback) where T : struct
    {
        T parsed;
        return Enum.TryParse(value, true, out parsed) ? parsed : fallback;
    }

    static int Clamp(int value, int minimum, int maximum)
    {
        return Math.Max(minimum, Math.Min(maximum, value));
    }
}

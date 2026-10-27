using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// Runs without touching the mouse or keyboard: pure logic is asserted, UI is rendered to PNG files in tests\out for inspection.
static class SelfTests
{
    const int OffScreenCoordinate = -20000;
    const int ShowNoActivate = 4;
    const int HideWindow = 0;

    static readonly string OutputFolder = Path.Combine("tests", "out");
    static readonly List<string> Failures = new List<string>();
    static int checkCount;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr windowHandle, int command);

    [STAThread]
    static int Main()
    {
        NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
        Application.EnableVisualStyles();
        Directory.CreateDirectory(OutputFolder);

        Run("Hotkey parsing", TestHotkeys);
        Run("Settings file", TestSettingsStore);
        Run("Output files", TestOutputFiles);
        Run("Localisation", TestLocalisation);
        Run("GIF encoder", TestGifEncoder);
        Run("Annotations", TestAnnotations);
        Run("Screen capture", TestScreenCapture);
        Run("Shutter sound", TestShutterSound);
        Run("OCR", TestOcr);
        Run("Rendered forms", TestRenderedForms);

        Console.WriteLine();
        Console.WriteLine("{0} checks, {1} failed", checkCount, Failures.Count);
        foreach (string failure in Failures) Console.WriteLine("  FAILED: " + failure);
        return Failures.Count == 0 ? 0 : 1;
    }

    static void Run(string name, Action test)
    {
        Console.WriteLine("== " + name);
        try
        {
            test();
        }
        catch (Exception error)
        {
            Check(false, name + " threw " + error.GetType().Name + ": " + error.Message);
        }
    }

    static void Check(bool condition, string description)
    {
        checkCount++;
        if (!condition) Failures.Add(description);
        Console.WriteLine("  {0} {1}", condition ? "ok  " : "FAIL", description);
    }

    static void TestHotkeys()
    {
        Hotkey parsed = Hotkey.Parse("Ctrl+Shift+4");
        Check(parsed != null && parsed.Modifiers == (HotkeyModifiers.Control | HotkeyModifiers.Shift) && parsed.Key == Keys.D4, "parses Ctrl+Shift+4");
        Check(parsed.ToString() == "Ctrl+Shift+4", "formats digits without the D prefix");
        Check(Hotkey.Parse("alt+f9").ToString() == "Alt+F9", "parses case-insensitively");
        Check(Hotkey.Parse("Ctrl+Alt+Shift+A").Equals(new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, Keys.A)), "round-trips three modifiers");
        foreach (string invalid in new[] { "4", "Ctrl+", "Foo+4", "Ctrl+Shift", "", "Ctrl+Shift+Nonsense" })
            Check(Hotkey.Parse(invalid) == null, "rejects \"" + invalid + "\"");
    }

    static void TestSettingsStore()
    {
        string path = Path.Combine(Path.GetTempPath(), "snapshot-test-" + Guid.NewGuid() + ".ini");
        try
        {
            AppSettings settings = AppSettings.CreateDefault();
            settings.SaveFolder = @"C:\Temp\My Shots=1";
            settings.FileNameTemplate = "Snap {date}";
            settings.Format = ImageFileFormat.Jpg;
            settings.JpegQuality = 70;
            settings.CopyToClipboard = true;
            settings.PlayShutterSound = false;
            settings.IncludeCursor = true;
            settings.ShowMagnifier = true;
            settings.DelaySeconds = 5;
            settings.Language = "th";
            settings.RecordFormat = RecordingFormat.Mp4;
            settings.RecordFramesPerSecond = 20;
            settings.RecordMaxSeconds = 90;
            settings.SetHotkey(CaptureAction.Region, new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.F9));

            SettingsStore.Save(settings, path);
            AppSettings loaded = SettingsStore.Load(path);
            Check(loaded.SaveFolder == settings.SaveFolder, "keeps a path containing '='");
            Check(loaded.FileNameTemplate == "Snap {date}" && loaded.Format == ImageFileFormat.Jpg && loaded.JpegQuality == 70, "round-trips template, format, quality");
            Check(loaded.CopyToClipboard && !loaded.PlayShutterSound && loaded.IncludeCursor && loaded.ShowMagnifier && loaded.DelaySeconds == 5, "round-trips flags and delay");
            Check(!AppSettings.CreateDefault().ShowMagnifier && !AppSettings.CreateDefault().IncludeCursor, "magnifier and cursor are off by default");
            Check(loaded.Language == "th" && loaded.RecordFormat == RecordingFormat.Mp4 && loaded.RecordFramesPerSecond == 20 && loaded.RecordMaxSeconds == 90, "round-trips language and recording options");
            Check(loaded.GetHotkey(CaptureAction.Region).Equals(new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.F9)), "round-trips a custom hotkey");

            File.WriteAllLines(path, new[] { "JpegQuality=999", "DelaySeconds=abc", "Hotkey.Region=nonsense", "Language=fr", "garbage line", "Format=Gif", "RecordFramesPerSecond=0" });
            AppSettings defaults = AppSettings.CreateDefault();
            AppSettings repaired = SettingsStore.Load(path);
            Check(repaired.JpegQuality == 100, "clamps an out-of-range number");
            Check(repaired.DelaySeconds == defaults.DelaySeconds && repaired.Language == defaults.Language && repaired.Format == defaults.Format, "ignores unparsable values");
            Check(repaired.GetHotkey(CaptureAction.Region).Equals(defaults.GetHotkey(CaptureAction.Region)), "ignores an invalid hotkey");
            Check(repaired.RecordFramesPerSecond == 1, "clamps frames per second to at least 1");
            Check(SettingsStore.Load(path + ".missing").SaveFolder == defaults.SaveFolder, "a missing file yields defaults");

            AppSettings copy = settings.Clone();
            copy.SetHotkey(CaptureAction.Region, new Hotkey(HotkeyModifiers.Shift, Keys.F1));
            Check(settings.GetHotkey(CaptureAction.Region).Key == Keys.F9, "Clone does not share the hotkey table");

            var seen = new HashSet<Hotkey>();
            Check(AppSettings.AllActions.All(action => seen.Add(defaults.GetHotkey(action))), "default hotkeys are unique");
        }
        finally
        {
            File.Delete(path);
        }
    }

    static void TestOutputFiles()
    {
        var moment = new DateTime(2026, 10, 22, 1, 2, 3);
        Check(OutputFiles.BuildFileName("Shot {date} at {time}", moment) == "Shot 2026-10-22 at 01.02.03", "fills {date} and {time}");
        Check(OutputFiles.BuildFileName("a:b<c>", moment) == "a_b_c_", "replaces characters Windows forbids");
        Check(OutputFiles.BuildFileName("   ", moment) == "Screenshot", "falls back for an empty template");

        string folder = Path.Combine(Path.GetTempPath(), "snapshot-test-" + Guid.NewGuid());
        try
        {
            AppSettings settings = AppSettings.CreateDefault();
            settings.SaveFolder = folder;
            settings.FileNameTemplate = "fixed";
            using (var image = new Bitmap(20, 10))
            {
                string first = OutputFiles.SaveScreenshot(image, settings);
                string second = OutputFiles.SaveScreenshot(image, settings);
                Check(Path.GetFileName(first) == "fixed.png" && Path.GetFileName(second) == "fixed (2).png", "numbers duplicate names");

                settings.Format = ImageFileFormat.Jpg;
                byte[] jpeg = File.ReadAllBytes(OutputFiles.SaveScreenshot(image, settings));
                Check(jpeg[0] == 0xFF && jpeg[1] == 0xD8, "writes a real JPEG");
                settings.Format = ImageFileFormat.Bmp;
                Check(Path.GetExtension(OutputFiles.SaveScreenshot(image, settings)) == ".bmp", "writes a BMP");
            }
            Check(Path.GetFileName(OutputFiles.NextRecordingPath(settings, ".gif")).StartsWith("Screen Recording "), "recordings use their own name");
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    static void TestLocalisation()
    {
        var usedKeys = new HashSet<string>();
        foreach (string file in Directory.GetFiles("src", "*.cs", SearchOption.AllDirectories))
            foreach (Match match in Regex.Matches(File.ReadAllText(file), "Loc\\.T\\(\"([^\"]+)\"\\)"))
                usedKeys.Add(match.Groups[1].Value);
        foreach (CaptureAction action in AppSettings.AllActions) usedKeys.Add("action." + action);
        foreach (MarkupTool tool in Enum.GetValues(typeof(MarkupTool))) usedKeys.Add("markup.tool." + tool);

        var known = new HashSet<string>(Loc.Keys);
        string[] missing = usedKeys.Where(key => !known.Contains(key)).ToArray();
        Check(missing.Length == 0, "every key used in code has a translation" + (missing.Length == 0 ? "" : ": " + string.Join(", ", missing)));
        Check(usedKeys.Count > 50, "found " + usedKeys.Count + " keys in use");

        Loc.Language = "th";
        Check(Loc.T("common.ok") == "ตกลง", "Thai text comes through the compiler intact");
        Loc.Language = "en";
        Check(Loc.T("common.ok") == "OK" && Loc.T("no.such.key") == "no.such.key", "English text, unknown keys echo back");
    }

    static void TestGifEncoder()
    {
        const int size = 128;
        var random = new Random(7);
        var noisy = new Bitmap(size, size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                noisy.SetPixel(x, y, Color.FromArgb(random.Next(256), random.Next(256), random.Next(256)));
        Bitmap withBlue = Paint(noisy, Color.Blue, new Rectangle(10, 10, 30, 20));
        Bitmap withGreen = Paint(withBlue, Color.Lime, new Rectangle(70, 70, 20, 20));

        string path = Path.Combine(Path.GetTempPath(), "snapshot-test-" + Guid.NewGuid() + ".gif");
        try
        {
            using (FileStream stream = File.Create(path))
            {
                var encoder = new GifEncoder(stream, size, size);
                encoder.AddFrame(noisy, TimeSpan.FromMilliseconds(0));
                encoder.AddFrame(withBlue, TimeSpan.FromMilliseconds(100));
                encoder.AddFrame(withBlue, TimeSpan.FromMilliseconds(200)); // identical, must be merged into the previous frame
                encoder.AddFrame(withGreen, TimeSpan.FromMilliseconds(300));
                encoder.Finish(TimeSpan.FromMilliseconds(500));
            }

            using (Image gif = Image.FromFile(path))
            {
                Check(gif.GetFrameCount(FrameDimension.Time) == 3, "an unchanged frame is merged (3 frames from 4)");
                byte[] delays = gif.GetPropertyItem(0x5100).Value;
                Check(BitConverter.ToInt32(delays, 0) == 10 && BitConverter.ToInt32(delays, 4) == 20 && BitConverter.ToInt32(delays, 8) == 20, "frame delays follow the timestamps (merged frame lasts 200 ms)");

                gif.SelectActiveFrame(FrameDimension.Time, 0);
                int wrongPixels = CountPixelsDifferingFromQuantized(new Bitmap(gif), noisy);
                Check(wrongPixels == 0, "LZW round-trips a noisy frame exactly (" + wrongPixels + " wrong of " + size * size + ")");

                gif.SelectActiveFrame(FrameDimension.Time, 1);
                Check(NearlyEqual(new Bitmap(gif).GetPixel(20, 15), Color.Blue), "second frame holds the blue square");
                gif.SelectActiveFrame(FrameDimension.Time, 2);
                Check(NearlyEqual(new Bitmap(gif).GetPixel(80, 80), Color.Lime), "third frame holds the green square");
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    static Bitmap Paint(Bitmap original, Color color, Rectangle area)
    {
        var copy = new Bitmap(original);
        using (Graphics graphics = Graphics.FromImage(copy))
        using (var brush = new SolidBrush(color))
            graphics.FillRectangle(brush, area);
        return copy;
    }

    static int CountPixelsDifferingFromQuantized(Bitmap decoded, Bitmap original)
    {
        int wrong = 0;
        for (int y = 0; y < original.Height; y++)
            for (int x = 0; x < original.Width; x++)
            {
                Color expected = Quantize(original.GetPixel(x, y));
                Color actual = decoded.GetPixel(x, y);
                if (expected.R != actual.R || expected.G != actual.G || expected.B != actual.B) wrong++;
            }
        return wrong;
    }

    // Mirrors the encoder's fixed 6x7x6 palette.
    static Color Quantize(Color color)
    {
        return Color.FromArgb(Level(color.R, 6) * 255 / 5, Level(color.G, 7) * 255 / 6, Level(color.B, 6) * 255 / 5);
    }

    static int Level(int value, int levels)
    {
        return (value * (levels - 1) + 127) / 255;
    }

    static bool NearlyEqual(Color first, Color second)
    {
        return Math.Abs(first.R - second.R) < 3 && Math.Abs(first.G - second.G) < 3 && Math.Abs(first.B - second.B) < 3;
    }

    static void TestAnnotations()
    {
        using (var canvas = new Bitmap(360, 200))
        {
            using (Graphics graphics = Graphics.FromImage(canvas))
            {
                graphics.Clear(Color.White);
                for (int x = 0; x < 360; x += 12)
                    graphics.DrawString("secret", new Font("Segoe UI", 12f), Brushes.Black, x % 120 + 200, (x / 120) * 30 + 20);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                var pen = new PenAnnotation(Color.Red, 4f, new Point(10, 10));
                pen.Drag(new Point(10, 10), new Point(60, 40));
                pen.Drag(new Point(10, 10), new Point(110, 20));
                var arrow = new ArrowAnnotation(Color.Blue, 5f);
                arrow.Drag(new Point(20, 120), new Point(170, 70));
                var rectangle = new RectangleAnnotation(Color.Green, 3f);
                rectangle.Drag(new Point(30, 140), new Point(150, 190));
                var ellipse = new EllipseAnnotation(Color.Orange, 3f);
                ellipse.Drag(new Point(160, 130), new Point(230, 190));
                var text = new TextAnnotation(Color.Purple, 4f, new Point(120, 10), "Hello");
                var blur = new PixelateAnnotation();
                blur.Drag(new Point(200, 10), new Point(350, 100));

                foreach (Annotation annotation in new Annotation[] { pen, arrow, rectangle, ellipse, text, blur })
                    annotation.Draw(graphics, new Bitmap(canvas));
            }
            canvas.Save(Path.Combine(OutputFolder, "annotations.png"));
            Check(canvas.GetPixel(60, 190).ToArgb() != Color.White.ToArgb() || canvas.GetPixel(30, 160).ToArgb() != Color.White.ToArgb(), "annotations changed the picture (see tests\\out\\annotations.png)");
        }
    }

    static void TestScreenCapture()
    {
        using (Bitmap withoutCursor = ScreenCapture.CopyArea(new Rectangle(0, 0, 200, 100), false))
        using (Bitmap withCursor = ScreenCapture.CopyArea(new Rectangle(0, 0, 200, 100), true))
            Check(withoutCursor.Size == new Size(200, 100) && withCursor.Size == new Size(200, 100), "CopyArea returns the requested size, with and without the cursor");
        Check(WindowFinder.GetVisibleWindowBounds().Count > 0, "finds visible windows on this desktop");
    }

    static void TestShutterSound()
    {
        byte[] wav = (byte[])typeof(ShutterSound).GetMethod("BuildWav", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        Check(wav.Length > 44 && System.Text.Encoding.ASCII.GetString(wav, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(wav, 8, 4) == "WAVE", "synthesised sound is a valid WAV header (" + wav.Length + " bytes)");
        Check(wav.Skip(44).Any(value => value != 0), "synthesised sound is not silent");
    }

    static void TestOcr()
    {
        using (var image = new Bitmap(520, 120))
        {
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.White);
                graphics.DrawString("Hello World 12345", new Font("Segoe UI", 28f), Brushes.Black, 10, 25);
            }
            try
            {
                string text = OcrService.Recognize(image);
                Check(text.Contains("Hello") && text.Contains("12345"), "reads rendered text: [" + text + "]");
            }
            catch (InvalidOperationException error)
            {
                Console.WriteLine("  skip " + error.Message);
            }
        }
    }

    static void TestRenderedForms()
    {
        Loc.Language = "en";
        AppSettings settings = AppSettings.CreateDefault();

        using (var settingsForm = new SettingsForm(settings))
        {
            RenderTabs(settingsForm, "settings-en");
        }
        Loc.Language = "th";
        using (var settingsForm = new SettingsForm(settings))
        {
            RenderTabs(settingsForm, "settings-th");
        }
        Loc.Language = "en";

        string imagePath = Path.Combine(OutputFolder, "sample.png");
        using (Bitmap sample = BuildSampleScreenshot(900, 540))
        {
            sample.Save(imagePath);
            using (var editor = new MarkupEditorForm(imagePath, settings))
                RenderForm(editor, "markup-editor");

            using (var thumbnail = new ThumbnailForm(imagePath, sample, false, settings))
                RenderForm(thumbnail, "thumbnail-image");
            using (var recordingThumbnail = new ThumbnailForm(Path.Combine(OutputFolder, "sample.gif"), sample, true, settings))
                RenderForm(recordingThumbnail, "thumbnail-recording");

            RenderSelector(sample);
        }

        using (Icon idle = TrayIcons.CreateIdle())
        using (Icon recording = TrayIcons.CreateRecording())
        {
            SaveEnlarged(idle.ToBitmap(), "tray-idle");
            SaveEnlarged(recording.ToBitmap(), "tray-recording");
        }
        Check(true, "forms rendered to " + OutputFolder);
    }

    static void RenderTabs(SettingsForm form, string name)
    {
        var tabs = form.Controls.OfType<TabControl>().Single();
        for (int index = 0; index < tabs.TabPages.Count; index++)
        {
            tabs.SelectedIndex = index;
            RenderForm(form, name + "-tab" + (index + 1));
        }
    }

    static void RenderSelector(Bitmap sample)
    {
        var windows = new List<Rectangle> { new Rectangle(100, 80, 500, 300) };
        foreach (bool showMagnifier in new[] { true, false })
        {
            using (var selector = new RegionSelectorForm(sample, new Rectangle(0, 0, sample.Width, sample.Height), windows, SelectionMode.Region, showMagnifier))
            {
                SetField(selector, "cursorPoint", new Point(520, 300));
                SetField(selector, "selection", new Rectangle(200, 150, 320, 150));
                typeof(RegionSelectorForm).GetMethod("RefreshLoupe", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(selector, null);
                var loupeBounds = (Rectangle)selector.GetType().GetField("loupeBounds", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(selector);
                Check(loupeBounds.IsEmpty == !showMagnifier, "magnifier is " + (showMagnifier ? "shown" : "hidden") + " when the setting says so");
                RenderForm(selector, showMagnifier ? "selector-region" : "selector-region-no-magnifier");
            }
        }
        using (var selector = new RegionSelectorForm(sample, new Rectangle(0, 0, sample.Width, sample.Height), windows, SelectionMode.Window, false))
        {
            SetField(selector, "cursorPoint", new Point(300, 200));
            typeof(RegionSelectorForm).GetMethod("UpdateHoveredWindow", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(selector, null);
            RenderForm(selector, "selector-window");
        }
    }

    static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }

    static void RenderForm(Form form, string name)
    {
        // Child controls only paint once the window is shown, so it is shown far off-screen without activation.
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(OffScreenCoordinate, OffScreenCoordinate);
        form.CreateControl();
        ShowWindow(form.Handle, ShowNoActivate);
        Application.DoEvents();
        form.PerformLayout();
        using (var bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
            bitmap.Save(Path.Combine(OutputFolder, name + ".png"));
        }
        ShowWindow(form.Handle, HideWindow);
    }

    static void SaveEnlarged(Bitmap icon, string name)
    {
        using (var enlarged = new Bitmap(icon.Width * 8, icon.Height * 8))
        using (Graphics graphics = Graphics.FromImage(enlarged))
        {
            graphics.Clear(Color.DimGray);
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.DrawImage(icon, 0, 0, enlarged.Width, enlarged.Height);
            enlarged.Save(Path.Combine(OutputFolder, name + ".png"));
        }
    }

    static Bitmap BuildSampleScreenshot(int width, int height)
    {
        var bitmap = new Bitmap(width, height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (var background = new LinearGradientBrush(new Rectangle(0, 0, width, height), Color.SteelBlue, Color.Lavender, 45f))
        {
            graphics.FillRectangle(background, 0, 0, width, height);
            graphics.FillRectangle(Brushes.White, 100, 80, 500, 300);
            graphics.DrawString("Sample window", new Font("Segoe UI", 16f), Brushes.Black, 120, 100);
            graphics.DrawString("Password: hunter2", new Font("Segoe UI", 12f), Brushes.DarkRed, 120, 160);
        }
        return bitmap;
    }
}

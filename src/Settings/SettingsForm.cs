using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

class SettingsForm : Form
{
    static readonly int[] DelayChoices = { 0, 3, 5, 10 };

    readonly AppSettings working;
    readonly TabControl tabs = new TabControl { Dock = DockStyle.Fill };

    readonly TextBox saveFolderBox = new TextBox { Dock = DockStyle.Fill };
    readonly TextBox templateBox = new TextBox { Dock = DockStyle.Fill };
    readonly Label previewLabel = new Label { AutoSize = true };
    readonly ComboBox formatBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly NumericUpDown jpegQualityBox = new NumericUpDown { Minimum = 1, Maximum = 100 };
    readonly ComboBox languageBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly CheckBox startupBox = new CheckBox { AutoSize = true };

    readonly CheckBox includeCursorBox = new CheckBox { AutoSize = true };
    readonly CheckBox showMagnifierBox = new CheckBox { AutoSize = true };
    readonly CheckBox shutterBox = new CheckBox { AutoSize = true };
    readonly CheckBox clipboardBox = new CheckBox { AutoSize = true };
    readonly ComboBox delayBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

    readonly Dictionary<CaptureAction, HotkeyBox> hotkeyBoxes = new Dictionary<CaptureAction, HotkeyBox>();

    readonly ComboBox recordFormatBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly NumericUpDown framesPerSecondBox = new NumericUpDown { Minimum = 1, Maximum = 30 };
    readonly NumericUpDown maxSecondsBox = new NumericUpDown { Minimum = 1, Maximum = 600 };

    public AppSettings Result { get { return working; } }

    public bool RunAtStartup { get { return startupBox.Checked; } }

    public SettingsForm(AppSettings current)
    {
        working = current.Clone();

        Text = Loc.T("settings.title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        ClientSize = new Size(560, 470);

        AddTab("settings.tab.general", BuildGeneralTab());
        AddTab("settings.tab.capture", BuildCaptureTab());
        AddTab("settings.tab.hotkeys", BuildHotkeysTab());
        AddTab("settings.tab.recording", BuildRecordingTab());

        Controls.Add(tabs);
        Controls.Add(BuildButtonBar());
        LoadValues();
    }

    void AddTab(string titleKey, Control content)
    {
        var page = new TabPage(Loc.T(titleKey)) { UseVisualStyleBackColor = true, AutoScroll = true };
        page.Controls.Add(content);
        tabs.TabPages.Add(page);
    }

    Control BuildGeneralTab()
    {
        TableLayoutPanel table = CreateTable();

        var browseButton = new Button { Text = Loc.T("settings.browse"), AutoSize = true };
        browseButton.Click += delegate { BrowseForFolder(); };
        AddRow(table, "settings.saveFolder", saveFolderBox);
        AddRow(table, null, browseButton);

        AddRow(table, "settings.fileName", templateBox);
        AddRow(table, "settings.preview", previewLabel);
        AddNote(table, Loc.T("settings.fileNameHint"));
        AddRow(table, "settings.format", formatBox);
        AddRow(table, "settings.jpegQuality", jpegQualityBox);
        AddRow(table, "settings.language", languageBox);
        AddRow(table, null, startupBox);
        startupBox.Text = Loc.T("settings.startup");

        formatBox.Items.AddRange(new object[] { "PNG", "JPG", "BMP" });
        languageBox.Items.AddRange(new object[] { "English", "ไทย" });
        templateBox.TextChanged += delegate { UpdatePreview(); };
        formatBox.SelectedIndexChanged += delegate
        {
            jpegQualityBox.Enabled = formatBox.SelectedIndex == (int)ImageFileFormat.Jpg;
            UpdatePreview();
        };
        return table;
    }

    Control BuildCaptureTab()
    {
        TableLayoutPanel table = CreateTable();
        includeCursorBox.Text = Loc.T("settings.includeCursor");
        showMagnifierBox.Text = Loc.T("settings.showMagnifier");
        shutterBox.Text = Loc.T("settings.shutter");
        clipboardBox.Text = Loc.T("settings.clipboard");
        AddRow(table, null, includeCursorBox);
        AddRow(table, null, showMagnifierBox);
        AddRow(table, null, shutterBox);
        AddRow(table, null, clipboardBox);
        AddRow(table, "settings.timer", delayBox);
        return table;
    }

    Control BuildHotkeysTab()
    {
        TableLayoutPanel table = CreateTable();
        AddNote(table, Loc.T("settings.hotkeysHint"));
        foreach (CaptureAction action in AppSettings.AllActions)
        {
            var box = new HotkeyBox { Dock = DockStyle.Fill };
            hotkeyBoxes[action] = box;
            AddRow(table, "action." + action, box);
        }

        var resetButton = new Button { Text = Loc.T("settings.hotkeysReset"), AutoSize = true };
        resetButton.Click += delegate { ResetHotkeyBoxes(); };
        AddRow(table, null, resetButton);
        return table;
    }

    Control BuildRecordingTab()
    {
        TableLayoutPanel table = CreateTable();
        recordFormatBox.Items.Add("GIF");
        bool hasFfmpeg = FfmpegEncoder.FindOnPath() != null;
        if (hasFfmpeg) recordFormatBox.Items.Add("MP4");

        AddRow(table, "settings.recordFormat", recordFormatBox);
        AddRow(table, "settings.recordFps", framesPerSecondBox);
        AddRow(table, "settings.recordMaxSeconds", maxSecondsBox);
        if (!hasFfmpeg) AddNote(table, Loc.T("settings.ffmpegMissing"));
        return table;
    }

    Control BuildButtonBar()
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(8) };
        var cancelButton = new Button { Text = Loc.T("common.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
        var okButton = new Button { Text = Loc.T("common.ok"), AutoSize = true };
        okButton.Click += delegate
        {
            if (TryApply()) DialogResult = DialogResult.OK;
        };
        bar.Controls.Add(cancelButton);
        bar.Controls.Add(okButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        return bar;
    }

    void LoadValues()
    {
        saveFolderBox.Text = working.SaveFolder;
        templateBox.Text = working.FileNameTemplate;
        formatBox.SelectedIndex = (int)working.Format;
        jpegQualityBox.Value = working.JpegQuality;
        languageBox.SelectedIndex = working.Language == "th" ? 1 : 0;
        startupBox.Checked = StartupRegistration.IsEnabled;

        includeCursorBox.Checked = working.IncludeCursor;
        showMagnifierBox.Checked = working.ShowMagnifier;
        shutterBox.Checked = working.PlayShutterSound;
        clipboardBox.Checked = working.CopyToClipboard;
        LoadDelayChoices();

        foreach (CaptureAction action in AppSettings.AllActions)
            hotkeyBoxes[action].Hotkey = working.GetHotkey(action);

        recordFormatBox.SelectedIndex = (int)working.RecordFormat < recordFormatBox.Items.Count ? (int)working.RecordFormat : 0;
        framesPerSecondBox.Value = working.RecordFramesPerSecond;
        maxSecondsBox.Value = working.RecordMaxSeconds;
    }

    // A hand-edited settings file may hold a delay that is not one of the menu choices; keep it selectable.
    void LoadDelayChoices()
    {
        List<int> choices = DelayChoices.Concat(new[] { working.DelaySeconds }).Distinct().OrderBy(seconds => seconds).ToList();
        foreach (int seconds in choices)
            delayBox.Items.Add(seconds == 0 ? Loc.T("menu.timer.off") : string.Format(Loc.T("menu.timer.seconds"), seconds));
        delayBox.SelectedIndex = choices.IndexOf(working.DelaySeconds);
        delayBox.Tag = choices;
    }

    void ResetHotkeyBoxes()
    {
        AppSettings defaults = AppSettings.CreateDefault();
        foreach (CaptureAction action in AppSettings.AllActions)
            hotkeyBoxes[action].Hotkey = defaults.GetHotkey(action);
    }

    void BrowseForFolder()
    {
        using (var dialog = new FolderBrowserDialog { Description = Loc.T("settings.browseDescription"), SelectedPath = saveFolderBox.Text })
        {
            if (dialog.ShowDialog(this) == DialogResult.OK) saveFolderBox.Text = dialog.SelectedPath;
        }
    }

    void UpdatePreview()
    {
        if (formatBox.SelectedIndex < 0) return;
        previewLabel.Text = OutputFiles.BuildFileName(templateBox.Text, DateTime.Now) + OutputFiles.ExtensionOf((ImageFileFormat)formatBox.SelectedIndex);
    }

    bool TryApply()
    {
        string folder = saveFolderBox.Text.Trim();
        string folderError = TryCreateFolder(folder);
        if (folderError != null) return Reject(string.Format(Loc.T("settings.error.folder"), folderError));
        if (templateBox.Text.Trim().Length == 0) return Reject(Loc.T("settings.error.template"));

        var seen = new HashSet<Hotkey>();
        foreach (CaptureAction action in AppSettings.AllActions)
        {
            Hotkey hotkey = hotkeyBoxes[action].Hotkey;
            if (!seen.Add(hotkey)) return Reject(string.Format(Loc.T("settings.error.duplicateHotkey"), hotkey));
        }

        working.SaveFolder = folder;
        working.FileNameTemplate = templateBox.Text.Trim();
        working.Format = (ImageFileFormat)formatBox.SelectedIndex;
        working.JpegQuality = (int)jpegQualityBox.Value;
        working.Language = languageBox.SelectedIndex == 1 ? "th" : "en";
        working.IncludeCursor = includeCursorBox.Checked;
        working.ShowMagnifier = showMagnifierBox.Checked;
        working.PlayShutterSound = shutterBox.Checked;
        working.CopyToClipboard = clipboardBox.Checked;
        working.DelaySeconds = ((List<int>)delayBox.Tag)[delayBox.SelectedIndex];
        foreach (CaptureAction action in AppSettings.AllActions)
            working.SetHotkey(action, hotkeyBoxes[action].Hotkey);
        if (recordFormatBox.Items.Count > 1) working.RecordFormat = (RecordingFormat)recordFormatBox.SelectedIndex; // otherwise keep a saved MP4 choice
        working.RecordFramesPerSecond = (int)framesPerSecondBox.Value;
        working.RecordMaxSeconds = (int)maxSecondsBox.Value;
        return true;
    }

    // Returns the problem text, or null when the folder exists or could be created.
    static string TryCreateFolder(string folder)
    {
        if (folder.Length == 0) return "-";
        try
        {
            Directory.CreateDirectory(folder);
            return null;
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

    bool Reject(string message)
    {
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    static TableLayoutPanel CreateTable()
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true, Padding = new Padding(12) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    // A null label key leaves the label column empty, so a checkbox lines up with the other inputs.
    static void AddRow(TableLayoutPanel table, string labelKey, Control control)
    {
        int row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label { Text = labelKey == null ? string.Empty : Loc.T(labelKey), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 16, 8) };
        control.Margin = new Padding(0, 4, 0, 4);
        table.Controls.Add(label, 0, row);
        table.Controls.Add(control, 1, row);
    }

    static void AddNote(TableLayoutPanel table, string text)
    {
        int row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var note = new Label { Text = text, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 4, 0, 8) };
        table.Controls.Add(note, 1, row);
    }
}

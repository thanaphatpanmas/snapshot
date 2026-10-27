using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Opens when a thumbnail is clicked, like Markup on macOS.
class MarkupEditorForm : Form
{
    const int ColorSwatchSize = 14;
    const double MaxScreenFraction = 0.9;
    static readonly string[] StrokeWidths = { "2", "4", "6", "10", "16" };

    readonly string filePath;
    readonly AppSettings settings;
    readonly MarkupCanvas canvas;
    readonly ToolStripButton[] toolButtons;
    readonly ToolStripButton colorButton = new ToolStripButton();
    bool isDirty;

    public MarkupEditorForm(string filePath, AppSettings settings)
    {
        this.filePath = filePath;
        this.settings = settings;
        Bitmap source = LoadWithoutLockingFile(filePath);
        canvas = new MarkupCanvas(source) { Dock = DockStyle.Fill, RequestText = PromptForText };
        canvas.Changed += delegate { isDirty = true; };

        Text = string.Format(Loc.T("markup.title"), Path.GetFileName(filePath));
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = ComputeInitialClientSize(source.Size);

        ToolStrip toolbar = BuildToolbar(out toolButtons);
        Controls.Add(canvas);
        Controls.Add(toolbar);
        SelectTool(MarkupTool.Pen);
        UpdateColorSwatch();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (isDirty && MessageBox.Show(this, Loc.T("markup.discard"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
            e.Cancel = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Control && e.KeyCode == Keys.Z) canvas.Undo();
        else if (e.Control && e.KeyCode == Keys.S) Save();
        else if (e.KeyCode == Keys.Escape) Close();
    }

    ToolStrip BuildToolbar(out ToolStripButton[] buttons)
    {
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, RenderMode = ToolStripRenderMode.System };

        MarkupTool[] tools = (MarkupTool[])Enum.GetValues(typeof(MarkupTool));
        buttons = new ToolStripButton[tools.Length];
        for (int index = 0; index < tools.Length; index++)
        {
            MarkupTool tool = tools[index];
            buttons[index] = new ToolStripButton(Loc.T("markup.tool." + tool)) { DisplayStyle = ToolStripItemDisplayStyle.Text };
            buttons[index].Click += delegate { SelectTool(tool); };
            toolbar.Items.Add(buttons[index]);
        }

        toolbar.Items.Add(new ToolStripSeparator());
        colorButton.Text = Loc.T("markup.color");
        colorButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
        colorButton.Click += delegate { ChooseColor(); };
        toolbar.Items.Add(colorButton);

        toolbar.Items.Add(new ToolStripLabel(Loc.T("markup.thickness")));
        var widthBox = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AutoSize = false, Width = DisplayScale.Scale(56) };
        widthBox.Items.AddRange(StrokeWidths);
        widthBox.SelectedIndex = 1;
        widthBox.SelectedIndexChanged += delegate { canvas.StrokeWidth = float.Parse(StrokeWidths[widthBox.SelectedIndex]); };
        toolbar.Items.Add(widthBox);

        toolbar.Items.Add(new ToolStripSeparator());
        AddAction(toolbar, "markup.undo", delegate { canvas.Undo(); });
        AddAction(toolbar, "markup.save", delegate { Save(); });
        AddAction(toolbar, "markup.saveCopy", delegate { SaveCopyAs(); });
        AddAction(toolbar, "markup.copy", delegate { CopyToClipboard(); });
        return toolbar;
    }

    static void AddAction(ToolStrip toolbar, string textKey, Action action)
    {
        var button = new ToolStripButton(Loc.T(textKey)) { DisplayStyle = ToolStripItemDisplayStyle.Text };
        button.Click += delegate { action(); };
        toolbar.Items.Add(button);
    }

    void SelectTool(MarkupTool selected)
    {
        canvas.Tool = selected;
        for (int index = 0; index < toolButtons.Length; index++)
            toolButtons[index].Checked = index == (int)selected;
    }

    void ChooseColor()
    {
        using (var dialog = new ColorDialog { Color = canvas.StrokeColor, FullOpen = true })
        {
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            canvas.StrokeColor = dialog.Color;
            UpdateColorSwatch();
        }
    }

    void UpdateColorSwatch()
    {
        int size = DisplayScale.Scale(ColorSwatchSize);
        var swatch = new Bitmap(size, size);
        using (Graphics graphics = Graphics.FromImage(swatch))
        {
            graphics.Clear(canvas.StrokeColor);
            graphics.DrawRectangle(Pens.Gray, 0, 0, size - 1, size - 1);
        }
        Image previous = colorButton.Image;
        colorButton.Image = swatch;
        if (previous != null) previous.Dispose();
    }

    string PromptForText()
    {
        using (var prompt = new TextPromptForm())
            return prompt.ShowDialog(this) == DialogResult.OK ? prompt.EnteredText : null;
    }

    void Save()
    {
        if (!TryWrite(filePath)) return;
        isDirty = false;
    }

    void SaveCopyAs()
    {
        string extension = Path.GetExtension(filePath);
        using (var dialog = new SaveFileDialog { FileName = Path.GetFileNameWithoutExtension(filePath) + " edited" + extension, Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp", InitialDirectory = Path.GetDirectoryName(filePath) })
        {
            if (dialog.ShowDialog(this) == DialogResult.OK) TryWrite(dialog.FileName);
        }
    }

    void CopyToClipboard()
    {
        using (Bitmap result = canvas.RenderResult())
            ClipboardHelper.SetImage(result);
    }

    bool TryWrite(string path)
    {
        try
        {
            using (Bitmap result = canvas.RenderResult())
                OutputFiles.SaveBitmap(result, path, settings);
            return true;
        }
        catch (IOException error)
        {
            MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (ExternalException error)
        {
            MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        return false;
    }

    // Decoding from a copy releases the file so Save can overwrite it.
    static Bitmap LoadWithoutLockingFile(string path)
    {
        using (var fromFile = new Bitmap(path))
            return new Bitmap(fromFile);
    }

    static Size ComputeInitialClientSize(Size imageSize)
    {
        Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
        int margin = DisplayScale.Scale(24);
        int width = Math.Max(DisplayScale.Scale(760), imageSize.Width + 2 * margin);
        int height = Math.Max(DisplayScale.Scale(480), imageSize.Height + 2 * margin + DisplayScale.Scale(40));
        return new Size(Math.Min(width, (int)(workArea.Width * MaxScreenFraction)), Math.Min(height, (int)(workArea.Height * MaxScreenFraction)));
    }
}

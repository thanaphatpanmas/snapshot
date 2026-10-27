using Microsoft.VisualBasic.FileIO;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

class ThumbnailForm : NonActivatingPopupForm
{
    const int MaxWidth = 240;
    const int MaxHeight = 160;
    const int ContentPadding = 8;
    const int DismissDelayMilliseconds = 6000;
    const int AnimationIntervalMilliseconds = 15;
    const double AnimationEasing = 0.3;

    readonly string filePath;
    readonly bool isRecording;
    readonly AppSettings settings;
    readonly Bitmap thumbnail;
    readonly Timer dismissTimer = new Timer { Interval = DismissDelayMilliseconds };
    readonly Timer animationTimer = new Timer { Interval = AnimationIntervalMilliseconds };
    readonly int padding;
    ContextMenuStrip contextMenu;
    Point targetLocation;
    Point mouseDownPoint;
    bool isLeftButtonDown;
    bool isMenuOpen;

    public ThumbnailForm(string filePath, Bitmap image, bool isRecording, AppSettings settings)
    {
        this.filePath = filePath;
        this.isRecording = isRecording;
        this.settings = settings;
        float dpiScale = DisplayScale.Factor;
        padding = DisplayScale.Scale(ContentPadding);
        thumbnail = CreateThumbnail(image, MaxWidth * dpiScale, MaxHeight * dpiScale);

        AllowTransparency = true; // layered from the start so it can be hidden mid-drag without recreating the window
        BackColor = Color.FromArgb(40, 40, 40);
        Cursor = Cursors.Hand;
        ClientSize = new Size(thumbnail.Width + 2 * padding, thumbnail.Height + 2 * padding);

        dismissTimer.Tick += delegate { Close(); };
        animationTimer.Tick += delegate { StepAnimation(); };
        MouseEnter += delegate { dismissTimer.Stop(); };
        MouseLeave += delegate { if (!isMenuOpen) dismissTimer.Start(); };
    }

    // Slides toward the target each tick, so new thumbnails glide in and older ones shift over smoothly.
    public void MoveTo(Point target)
    {
        targetLocation = target;
        animationTimer.Start();
    }

    public void StartOffscreenRight()
    {
        Location = new Point(Screen.PrimaryScreen.WorkingArea.Right, targetLocation.Y);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        dismissTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            thumbnail.Dispose();
            dismissTimer.Dispose();
            animationTimer.Dispose();
            if (contextMenu != null) contextMenu.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImage(thumbnail, new Rectangle(padding, padding, thumbnail.Width, thumbnail.Height));
        using (var border = new Pen(Color.FromArgb(110, 110, 110)))
            e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        if (isRecording) DrawRecordingBadge(e.Graphics);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Right)
        {
            ShowContextMenu(e.Location);
            return;
        }
        if (e.Button != MouseButtons.Left) return;
        isLeftButtonDown = true;
        mouseDownPoint = e.Location;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!isLeftButtonDown) return;

        Size dragSize = SystemInformation.DragSize;
        if (Math.Abs(e.X - mouseDownPoint.X) < dragSize.Width && Math.Abs(e.Y - mouseDownPoint.Y) < dragSize.Height) return;

        isLeftButtonDown = false;
        dismissTimer.Stop();
        if (DragThumbnailOut() == DragDropEffects.None)
        {
            Opacity = 1;
            dismissTimer.Start();
            return;
        }
        Close();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !isLeftButtonDown) return;
        isLeftButtonDown = false;
        if (isRecording) OpenInViewer();
        else OpenMarkupEditor();
        Close();
    }

    void StepAnimation()
    {
        int deltaX = targetLocation.X - Left;
        int deltaY = targetLocation.Y - Top;
        if (Math.Abs(deltaX) <= 1 && Math.Abs(deltaY) <= 1)
        {
            Location = targetLocation;
            animationTimer.Stop();
            return;
        }
        Location = new Point(Left + EasedStep(deltaX), Top + EasedStep(deltaY));
    }

    static int EasedStep(int distance)
    {
        return Math.Sign(distance) * (int)Math.Ceiling(Math.Abs(distance) * AnimationEasing);
    }

    // Windows draws no image for plain file drags, so a click-through ghost follows the cursor like on macOS.
    DragDropEffects DragThumbnailOut()
    {
        Point imageOrigin = new Point(Left + padding, Top + padding);
        Point grabOffset = new Point(Cursor.Position.X - imageOrigin.X, Cursor.Position.Y - imageOrigin.Y);
        using (var ghost = new DragGhostForm(thumbnail, imageOrigin))
        {
            GiveFeedbackEventHandler followCursor = delegate
            {
                ghost.Location = new Point(Cursor.Position.X - grabOffset.X, Cursor.Position.Y - grabOffset.Y);
            };
            GiveFeedback += followCursor;
            ghost.Show();
            Opacity = 0;
            try
            {
                return DoDragDrop(new DataObject(DataFormats.FileDrop, new[] { filePath }), DragDropEffects.Copy);
            }
            finally
            {
                GiveFeedback -= followCursor;
            }
        }
    }

    void ShowContextMenu(Point location)
    {
        dismissTimer.Stop();
        isMenuOpen = true;
        if (contextMenu != null) contextMenu.Dispose();

        contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(Loc.T("thumb.copy"), null, delegate { CopyToClipboard(); });
        contextMenu.Items.Add(Loc.T("thumb.open"), null, delegate { OpenInViewer(); });
        if (!isRecording)
            contextMenu.Items.Add(Loc.T("thumb.edit"), null, delegate { OpenMarkupEditor(); CloseAfterMenu(); });
        contextMenu.Items.Add(Loc.T("thumb.showInFolder"), null, delegate { ShowInFolder(); });
        contextMenu.Items.Add(Loc.T("thumb.saveAs"), null, delegate { SaveCopyAs(); });
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(Loc.T("thumb.delete"), null, delegate { MoveToRecycleBin(); CloseAfterMenu(); });
        contextMenu.Items.Add(Loc.T("thumb.dismiss"), null, delegate { CloseAfterMenu(); });
        contextMenu.Closed += delegate
        {
            isMenuOpen = false;
            if (!IsDisposed && !ClientRectangle.Contains(PointToClient(Cursor.Position))) dismissTimer.Start();
        };
        contextMenu.Show(this, location);
    }

    // Closing straight from a menu click would dispose the menu that is still running the handler.
    void CloseAfterMenu()
    {
        BeginInvoke(new Action(Close));
    }

    void CopyToClipboard()
    {
        if (isRecording)
        {
            ClipboardHelper.SetFile(filePath);
            return;
        }
        using (var image = new Bitmap(filePath))
            ClipboardHelper.SetImage(image);
    }

    void OpenInViewer()
    {
        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
    }

    void OpenMarkupEditor()
    {
        new MarkupEditorForm(filePath, settings).Show();
    }

    void ShowInFolder()
    {
        Process.Start("explorer.exe", "/select,\"" + filePath + "\"");
    }

    void SaveCopyAs()
    {
        string extension = Path.GetExtension(filePath);
        using (var dialog = new SaveFileDialog { FileName = Path.GetFileName(filePath), Filter = "*" + extension + "|*" + extension, InitialDirectory = Path.GetDirectoryName(filePath) })
        {
            if (dialog.ShowDialog() == DialogResult.OK)
                File.Copy(filePath, dialog.FileName, true);
        }
    }

    void MoveToRecycleBin()
    {
        FileSystem.DeleteFile(filePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
    }

    void DrawRecordingBadge(Graphics graphics)
    {
        string label = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
        using (var font = new Font("Segoe UI", 8f, FontStyle.Bold))
        {
            Size textSize = TextRenderer.MeasureText(label, font);
            var badgeBounds = new Rectangle(padding + 4, Height - padding - textSize.Height - 4, textSize.Width + 8, textSize.Height + 2);
            using (var background = new SolidBrush(Color.FromArgb(210, 200, 30, 30)))
                graphics.FillRectangle(background, badgeBounds);
            TextRenderer.DrawText(graphics, label, font, badgeBounds, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    static Bitmap CreateThumbnail(Bitmap image, float maxWidth, float maxHeight)
    {
        float scale = Math.Min(1f, Math.Min(maxWidth / image.Width, maxHeight / image.Height));
        var thumbnail = new Bitmap(Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale)));
        using (Graphics graphics = Graphics.FromImage(thumbnail))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(image, new Rectangle(0, 0, thumbnail.Width, thumbnail.Height));
        }
        return thumbnail;
    }
}

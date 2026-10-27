using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

enum SelectionMode
{
    Region,
    Window
}

// Full-screen overlay over a frozen screenshot: drag an area, or press Space to pick a whole window.
class RegionSelectorForm : Form
{
    const int MinimumSelectionSize = 5;
    const int LoupePixels = 11;
    const int LoupePixelSize = 10;
    const int LoupeCaptionHeight = 26;
    const int LoupeCursorGap = 24;
    const int HintTopMargin = 16;
    const int HintPadding = 10;
    static readonly Size SelectionRepaintPadding = new Size(120, 40);
    static readonly Color WindowHighlightColor = Color.FromArgb(0, 120, 215);

    readonly Bitmap screenshot;
    readonly Bitmap dimmedScreenshot;
    readonly Rectangle virtualScreen;
    readonly List<Rectangle> windowBounds;
    readonly bool showMagnifier;
    SelectionMode mode;
    Point cursorPoint;
    Point dragStart;
    bool isDragging;
    Rectangle selection;
    Rectangle hoveredWindow;
    Rectangle loupeBounds;
    string loupeNotice;

    public Rectangle SelectedRegion { get { return selection; } }

    public RegionSelectorForm(Bitmap screenshot, Rectangle virtualScreen, List<Rectangle> windowScreenBounds, SelectionMode initialMode, bool showMagnifier)
    {
        this.screenshot = screenshot;
        this.virtualScreen = virtualScreen;
        this.showMagnifier = showMagnifier;
        mode = initialMode;
        windowBounds = windowScreenBounds
            .Select(bounds => new Rectangle(bounds.X - virtualScreen.X, bounds.Y - virtualScreen.Y, bounds.Width, bounds.Height))
            .ToList();
        dimmedScreenshot = CreateDimmedCopy(screenshot);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = virtualScreen;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        KeyPreview = true;
        Cursor = Cursors.Cross;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Bounds = virtualScreen; // DPI changes can resize the form after it is first shown
        Activate();
        cursorPoint = PointToClient(Cursor.Position);
        if (mode == SelectionMode.Window) UpdateHoveredWindow();
        RefreshLoupe();
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) dimmedScreenshot.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Escape:
                DialogResult = DialogResult.Cancel;
                break;
            case Keys.Space:
                ToggleMode();
                break;
            case Keys.C:
                CopyColorUnderCursor();
                break;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        if (mode == SelectionMode.Window)
        {
            if (hoveredWindow.IsEmpty) return;
            selection = Rectangle.Intersect(hoveredWindow, ClientRectangle);
            DialogResult = DialogResult.OK;
            return;
        }
        dragStart = e.Location;
        isDragging = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        cursorPoint = e.Location;
        loupeNotice = null;

        Rectangle previousHighlight = CurrentHighlight();
        if (mode == SelectionMode.Window) UpdateHoveredWindow();
        else if (isDragging) UpdateSelection(e.Location);

        Rectangle dirtyArea = Rectangle.Union(previousHighlight, CurrentHighlight());
        dirtyArea.Inflate(SelectionRepaintPadding);
        Invalidate(dirtyArea);
        RefreshLoupe();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!isDragging) return;
        isDragging = false;
        bool isBigEnough = selection.Width >= MinimumSelectionSize && selection.Height >= MinimumSelectionSize;
        DialogResult = isBigEnough ? DialogResult.OK : DialogResult.Cancel;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.DrawImage(dimmedScreenshot, ClientRectangle);

        Rectangle highlight = CurrentHighlight();
        if (highlight.Width > 0 && highlight.Height > 0)
        {
            graphics.DrawImage(screenshot, highlight, highlight, GraphicsUnit.Pixel);
            DrawOutline(graphics, highlight);
            DrawSizeLabel(graphics, highlight);
        }
        if (!loupeBounds.IsEmpty) DrawLoupe(graphics);
        DrawHint(graphics);
    }

    Rectangle CurrentHighlight()
    {
        return mode == SelectionMode.Window ? hoveredWindow : selection;
    }

    void ToggleMode()
    {
        mode = mode == SelectionMode.Region ? SelectionMode.Window : SelectionMode.Region;
        isDragging = false;
        selection = Rectangle.Empty;
        hoveredWindow = Rectangle.Empty;
        if (mode == SelectionMode.Window) UpdateHoveredWindow();
        RefreshLoupe();
        Invalidate();
    }

    void UpdateHoveredWindow()
    {
        hoveredWindow = windowBounds.FirstOrDefault(bounds => bounds.Contains(cursorPoint));
    }

    void UpdateSelection(Point current)
    {
        Rectangle dragged = Rectangle.FromLTRB(
            Math.Min(dragStart.X, current.X), Math.Min(dragStart.Y, current.Y),
            Math.Max(dragStart.X, current.X), Math.Max(dragStart.Y, current.Y));
        selection = Rectangle.Intersect(dragged, ClientRectangle);
    }

    void CopyColorUnderCursor()
    {
        if (!showMagnifier || mode != SelectionMode.Region) return;
        string hex = ColorToHex(PixelAt(cursorPoint));
        ClipboardHelper.SetText(hex);
        loupeNotice = string.Format(Loc.T("selector.colorCopied"), hex);
        Invalidate(loupeBounds);
    }

    Color PixelAt(Point point)
    {
        int x = Math.Max(0, Math.Min(screenshot.Width - 1, point.X));
        int y = Math.Max(0, Math.Min(screenshot.Height - 1, point.Y));
        return screenshot.GetPixel(x, y);
    }

    static string ColorToHex(Color color)
    {
        return string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
    }

    void RefreshLoupe()
    {
        Rectangle previous = loupeBounds;
        loupeBounds = showMagnifier && mode == SelectionMode.Region ? ComputeLoupeBounds() : Rectangle.Empty;
        if (!previous.IsEmpty) Invalidate(previous);
        if (!loupeBounds.IsEmpty) Invalidate(loupeBounds);
    }

    Rectangle ComputeLoupeBounds()
    {
        int pixelSize = DisplayScale.Scale(LoupePixelSize);
        int size = pixelSize * LoupePixels;
        int height = size + DisplayScale.Scale(LoupeCaptionHeight);
        int gap = DisplayScale.Scale(LoupeCursorGap);

        int x = cursorPoint.X + gap;
        int y = cursorPoint.Y + gap;
        if (x + size > ClientSize.Width) x = cursorPoint.X - gap - size;
        if (y + height > ClientSize.Height) y = cursorPoint.Y - gap - height;
        return new Rectangle(x, y, size, height);
    }

    void DrawOutline(Graphics graphics, Rectangle bounds)
    {
        bool isWindow = mode == SelectionMode.Window;
        using (var outline = new Pen(isWindow ? WindowHighlightColor : Color.White, isWindow ? DisplayScale.Scale(3) : 1))
            graphics.DrawRectangle(outline, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    void DrawSizeLabel(Graphics graphics, Rectangle bounds)
    {
        string text = bounds.Width + " x " + bounds.Height;
        using (var font = new Font("Segoe UI", 10f))
        {
            Size textSize = TextRenderer.MeasureText(text, font);
            int labelTop = bounds.Top - textSize.Height - 4;
            if (labelTop < 0) labelTop = bounds.Top + 4;
            var labelBounds = new Rectangle(bounds.Left, labelTop, textSize.Width, textSize.Height);
            graphics.FillRectangle(Brushes.Black, labelBounds);
            TextRenderer.DrawText(graphics, text, font, labelBounds, Color.White);
        }
    }

    void DrawLoupe(Graphics graphics)
    {
        int half = LoupePixels / 2;
        var squareBounds = new Rectangle(loupeBounds.X, loupeBounds.Y, loupeBounds.Width, loupeBounds.Width);
        var sourceBounds = new Rectangle(cursorPoint.X - half, cursorPoint.Y - half, LoupePixels, LoupePixels);

        graphics.FillRectangle(Brushes.Black, loupeBounds);
        GraphicsState state = graphics.Save();
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.DrawImage(screenshot, squareBounds, sourceBounds, GraphicsUnit.Pixel);
        graphics.Restore(state);

        int cellSize = squareBounds.Width / LoupePixels;
        using (var cellOutline = new Pen(Color.White))
            graphics.DrawRectangle(cellOutline, squareBounds.X + half * cellSize, squareBounds.Y + half * cellSize, cellSize, cellSize);
        using (var frame = new Pen(Color.White, 2))
            graphics.DrawRectangle(frame, loupeBounds.X, loupeBounds.Y, loupeBounds.Width - 1, loupeBounds.Height - 1);

        Color color = PixelAt(cursorPoint);
        string caption = loupeNotice ?? string.Format("{0}  {1},{2},{3}", ColorToHex(color), color.R, color.G, color.B);
        var captionBounds = new Rectangle(loupeBounds.X, squareBounds.Bottom, loupeBounds.Width, loupeBounds.Bottom - squareBounds.Bottom);
        using (var font = new Font("Segoe UI", 8f))
            TextRenderer.DrawText(graphics, caption, font, captionBounds, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    void DrawHint(Graphics graphics)
    {
        string hint = Loc.T(mode == SelectionMode.Region ? "selector.hint.region" : "selector.hint.window");
        if (showMagnifier && mode == SelectionMode.Region) hint += "  ·  " + Loc.T("selector.hint.copyColor");
        Rectangle primary = Screen.PrimaryScreen.Bounds;
        using (var font = new Font("Segoe UI", 10f))
        {
            Size textSize = TextRenderer.MeasureText(hint, font);
            int padding = DisplayScale.Scale(HintPadding);
            int centerX = primary.X - virtualScreen.X + primary.Width / 2;
            var hintBounds = new Rectangle(centerX - textSize.Width / 2 - padding, primary.Y - virtualScreen.Y + DisplayScale.Scale(HintTopMargin),
                textSize.Width + 2 * padding, textSize.Height + padding);
            using (var background = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                graphics.FillRectangle(background, hintBounds);
            TextRenderer.DrawText(graphics, hint, font, hintBounds, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    static Bitmap CreateDimmedCopy(Bitmap source)
    {
        var dimmed = new Bitmap(source);
        using (Graphics graphics = Graphics.FromImage(dimmed))
        using (var overlay = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
            graphics.FillRectangle(overlay, 0, 0, dimmed.Width, dimmed.Height);
        return dimmed;
    }
}

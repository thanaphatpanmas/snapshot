using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

enum MarkupTool
{
    Pen,
    Arrow,
    Rectangle,
    Ellipse,
    Text,
    Blur
}

// Shows the image scaled to fit and records annotations in image coordinates.
class MarkupCanvas : Control
{
    readonly Bitmap source;
    readonly List<Annotation> annotations = new List<Annotation>();
    Annotation inProgress;
    Point dragStart;
    float zoom = 1f;
    Point origin;

    public MarkupTool Tool { get; set; }
    public Color StrokeColor { get; set; }
    public float StrokeWidth { get; set; }
    public Func<string> RequestText { get; set; }
    public event EventHandler Changed;

    public bool HasAnnotations { get { return annotations.Count > 0; } }

    public MarkupCanvas(Bitmap source)
    {
        this.source = source;
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(45, 45, 45);
        Cursor = Cursors.Cross;
        StrokeColor = Color.Red;
        StrokeWidth = 4f;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) source.Dispose();
        base.Dispose(disposing);
    }

    public void Undo()
    {
        if (annotations.Count == 0) return;
        annotations.RemoveAt(annotations.Count - 1);
        Invalidate();
        RaiseChanged();
    }

    // The saved picture is the original pixels plus every annotation, never the scaled preview.
    public Bitmap RenderResult()
    {
        var result = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(result))
        {
            graphics.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height));
            DrawAnnotations(graphics);
        }
        return result;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        zoom = Math.Min(1f, Math.Min(ClientSize.Width / (float)source.Width, ClientSize.Height / (float)source.Height));
        if (zoom <= 0) zoom = 1f;
        origin = new Point((int)((ClientSize.Width - source.Width * zoom) / 2), (int)((ClientSize.Height - source.Height * zoom) / 2));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.TranslateTransform(origin.X, origin.Y);
        graphics.ScaleTransform(zoom, zoom);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height));
        DrawAnnotations(graphics);
        if (inProgress != null) DrawWithQuality(graphics, inProgress);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        dragStart = ToImagePoint(e.Location);
        if (Tool == MarkupTool.Text)
        {
            AddText(dragStart);
            return;
        }
        inProgress = CreateAnnotation(dragStart);
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (inProgress == null) return;
        inProgress.Drag(dragStart, ToImagePoint(e.Location));
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (inProgress == null) return;
        annotations.Add(inProgress);
        inProgress = null;
        Capture = false;
        Invalidate();
        RaiseChanged();
    }

    Point ToImagePoint(Point clientPoint)
    {
        return new Point((int)Math.Round((clientPoint.X - origin.X) / zoom), (int)Math.Round((clientPoint.Y - origin.Y) / zoom));
    }

    Annotation CreateAnnotation(Point start)
    {
        switch (Tool)
        {
            case MarkupTool.Arrow: return new ArrowAnnotation(StrokeColor, StrokeWidth);
            case MarkupTool.Rectangle: return new RectangleAnnotation(StrokeColor, StrokeWidth);
            case MarkupTool.Ellipse: return new EllipseAnnotation(StrokeColor, StrokeWidth);
            case MarkupTool.Blur: return new PixelateAnnotation();
            default: return new PenAnnotation(StrokeColor, StrokeWidth, start);
        }
    }

    void AddText(Point location)
    {
        string text = RequestText == null ? null : RequestText();
        if (string.IsNullOrWhiteSpace(text)) return;
        annotations.Add(new TextAnnotation(StrokeColor, StrokeWidth, location, text));
        Invalidate();
        RaiseChanged();
    }

    void DrawAnnotations(Graphics graphics)
    {
        foreach (Annotation annotation in annotations)
            DrawWithQuality(graphics, annotation);
    }

    void DrawWithQuality(Graphics graphics, Annotation annotation)
    {
        GraphicsState state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        annotation.Draw(graphics, source);
        graphics.Restore(state);
    }

    void RaiseChanged()
    {
        EventHandler handler = Changed;
        if (handler != null) handler(this, EventArgs.Empty);
    }
}

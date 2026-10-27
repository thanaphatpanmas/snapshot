using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

// Annotations are stored in image coordinates, so the editor preview and the saved file render identically.
abstract class Annotation
{
    protected readonly Color StrokeColor;
    protected readonly float StrokeWidth;

    protected Annotation(Color strokeColor, float strokeWidth)
    {
        StrokeColor = strokeColor;
        StrokeWidth = strokeWidth;
    }

    public abstract void Draw(Graphics graphics, Bitmap source);

    // Called while the mouse moves between the press point and the current point.
    public virtual void Drag(Point start, Point current)
    {
    }

    protected Pen CreateRoundPen()
    {
        return new Pen(StrokeColor, StrokeWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
    }

    protected static Rectangle RectangleBetween(Point first, Point second)
    {
        return Rectangle.FromLTRB(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y), Math.Max(first.X, second.X), Math.Max(first.Y, second.Y));
    }
}

class PenAnnotation : Annotation
{
    readonly List<Point> points = new List<Point>();

    public PenAnnotation(Color strokeColor, float strokeWidth, Point start) : base(strokeColor, strokeWidth)
    {
        points.Add(start);
    }

    public override void Drag(Point start, Point current)
    {
        points.Add(current);
    }

    public override void Draw(Graphics graphics, Bitmap source)
    {
        if (points.Count == 1)
        {
            using (var brush = new SolidBrush(StrokeColor))
                graphics.FillEllipse(brush, points[0].X - StrokeWidth / 2, points[0].Y - StrokeWidth / 2, StrokeWidth, StrokeWidth);
            return;
        }
        using (Pen pen = CreateRoundPen())
            graphics.DrawLines(pen, points.ToArray());
    }
}

class RectangleAnnotation : Annotation
{
    Rectangle bounds;

    public RectangleAnnotation(Color strokeColor, float strokeWidth) : base(strokeColor, strokeWidth)
    {
    }

    public override void Drag(Point start, Point current)
    {
        bounds = RectangleBetween(start, current);
    }

    public override void Draw(Graphics graphics, Bitmap source)
    {
        using (Pen pen = CreateRoundPen())
            graphics.DrawRectangle(pen, bounds);
    }
}

class EllipseAnnotation : Annotation
{
    Rectangle bounds;

    public EllipseAnnotation(Color strokeColor, float strokeWidth) : base(strokeColor, strokeWidth)
    {
    }

    public override void Drag(Point start, Point current)
    {
        bounds = RectangleBetween(start, current);
    }

    public override void Draw(Graphics graphics, Bitmap source)
    {
        using (Pen pen = CreateRoundPen())
            graphics.DrawEllipse(pen, bounds);
    }
}

class ArrowAnnotation : Annotation
{
    const double HeadHalfAngle = Math.PI / 7;

    Point tail;
    Point tip;

    public ArrowAnnotation(Color strokeColor, float strokeWidth) : base(strokeColor, strokeWidth)
    {
    }

    public override void Drag(Point start, Point current)
    {
        tail = start;
        tip = current;
    }

    public override void Draw(Graphics graphics, Bitmap source)
    {
        double length = Math.Sqrt(Math.Pow(tip.X - tail.X, 2) + Math.Pow(tip.Y - tail.Y, 2));
        if (length < 2) return;

        double angle = Math.Atan2(tip.Y - tail.Y, tip.X - tail.X);
        double headLength = Math.Min(length, Math.Max(14, StrokeWidth * 4));
        var headBase = new PointF((float)(tip.X - headLength * 0.8 * Math.Cos(angle)), (float)(tip.Y - headLength * 0.8 * Math.Sin(angle)));
        var headCorners = new[]
        {
            new PointF(tip.X, tip.Y),
            new PointF((float)(tip.X - headLength * Math.Cos(angle - HeadHalfAngle)), (float)(tip.Y - headLength * Math.Sin(angle - HeadHalfAngle))),
            new PointF((float)(tip.X - headLength * Math.Cos(angle + HeadHalfAngle)), (float)(tip.Y - headLength * Math.Sin(angle + HeadHalfAngle)))
        };

        using (Pen pen = CreateRoundPen())
            graphics.DrawLine(pen, tail, Point.Round(headBase));
        using (var brush = new SolidBrush(StrokeColor))
            graphics.FillPolygon(brush, headCorners);
    }
}

class TextAnnotation : Annotation
{
    const float SizePerStrokeUnit = 3f;
    const float BaseFontSize = 12f;

    readonly Point location;
    readonly string text;

    public TextAnnotation(Color strokeColor, float strokeWidth, Point location, string text) : base(strokeColor, strokeWidth)
    {
        this.location = location;
        this.text = text;
    }

    public override void Draw(Graphics graphics, Bitmap source)
    {
        using (var font = new Font("Segoe UI", BaseFontSize + StrokeWidth * SizePerStrokeUnit, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(StrokeColor))
            graphics.DrawString(text, font, brush, location);
    }
}

// Hides sensitive details: the area is shrunk and enlarged again without smoothing.
class PixelateAnnotation : Annotation
{
    const int BlockSize = 12;

    Rectangle bounds;

    public PixelateAnnotation() : base(Color.Empty, 0)
    {
    }

    public override void Drag(Point start, Point current)
    {
        bounds = RectangleBetween(start, current);
    }

    public override void Draw(Graphics graphics, Bitmap source)
    {
        Rectangle area = Rectangle.Intersect(bounds, new Rectangle(Point.Empty, source.Size));
        if (area.Width < 2 || area.Height < 2) return;

        using (var shrunk = new Bitmap(Math.Max(1, area.Width / BlockSize), Math.Max(1, area.Height / BlockSize)))
        {
            using (Graphics shrinkGraphics = Graphics.FromImage(shrunk))
            {
                shrinkGraphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                shrinkGraphics.DrawImage(source, new Rectangle(0, 0, shrunk.Width, shrunk.Height), area, GraphicsUnit.Pixel);
            }
            GraphicsState state = graphics.Save();
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(shrunk, area);
            graphics.Restore(state);
        }
    }
}

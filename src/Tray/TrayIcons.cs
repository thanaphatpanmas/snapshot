using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Drawn at runtime so the exe needs no embedded .ico resource.
static class TrayIcons
{
    public static Icon CreateIdle()
    {
        return Draw(delegate(Graphics graphics, int size)
        {
            float inset = size * 0.14f;
            float arm = size * 0.3f;
            float far = size - inset;
            using (var background = new SolidBrush(Color.FromArgb(48, 48, 48)))
                graphics.FillRectangle(background, 0, 0, size, size);
            using (var bracket = new Pen(Color.White, Math.Max(1.5f, size / 9f)))
            {
                graphics.DrawLines(bracket, new[] { new PointF(inset, inset + arm), new PointF(inset, inset), new PointF(inset + arm, inset) });
                graphics.DrawLines(bracket, new[] { new PointF(far - arm, inset), new PointF(far, inset), new PointF(far, inset + arm) });
                graphics.DrawLines(bracket, new[] { new PointF(inset, far - arm), new PointF(inset, far), new PointF(inset + arm, far) });
                graphics.DrawLines(bracket, new[] { new PointF(far - arm, far), new PointF(far, far), new PointF(far, far - arm) });
            }
        });
    }

    public static Icon CreateRecording()
    {
        return Draw(delegate(Graphics graphics, int size)
        {
            float margin = size * 0.12f;
            graphics.FillEllipse(Brushes.Red, margin, margin, size - 2 * margin, size - 2 * margin);
        });
    }

    static Icon Draw(Action<Graphics, int> paint)
    {
        int size = SystemInformation.SmallIconSize.Width;
        using (var bitmap = new Bitmap(size, size))
        {
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                paint(graphics, size);
            }
            IntPtr handle = bitmap.GetHicon();
            try
            {
                return (Icon)Icon.FromHandle(handle).Clone();
            }
            finally
            {
                NativeMethods.DestroyIcon(handle);
            }
        }
    }
}

using System;
using System.Drawing;
using System.Drawing.Imaging;

static class ScreenCapture
{
    public static Bitmap CopyArea(Rectangle area, bool includeCursor)
    {
        var bitmap = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(area.Location, Point.Empty, area.Size);
            if (includeCursor) DrawCursor(graphics, area);
        }
        return bitmap;
    }

    // CopyFromScreen never contains the mouse pointer, so it is painted on afterwards.
    static void DrawCursor(Graphics graphics, Rectangle area)
    {
        var cursorInfo = new NativeMethods.CursorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.CursorInfo)) };
        if (!NativeMethods.GetCursorInfo(ref cursorInfo) || (cursorInfo.Flags & NativeMethods.CursorShowing) == 0) return;

        NativeMethods.IconInfo iconInfo;
        if (!NativeMethods.GetIconInfo(cursorInfo.CursorHandle, out iconInfo)) return;

        int x = cursorInfo.ScreenPosition.X - area.X - iconInfo.HotspotX;
        int y = cursorInfo.ScreenPosition.Y - area.Y - iconInfo.HotspotY;
        IntPtr deviceContext = graphics.GetHdc();
        try
        {
            NativeMethods.DrawIconEx(deviceContext, x, y, cursorInfo.CursorHandle, 0, 0, 0, IntPtr.Zero, NativeMethods.DrawIconNormal);
        }
        finally
        {
            graphics.ReleaseHdc(deviceContext);
            NativeMethods.DeleteObject(iconInfo.MaskBitmap);
            NativeMethods.DeleteObject(iconInfo.ColorBitmap);
        }
    }
}

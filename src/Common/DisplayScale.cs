using System;

// Forms are sized in pixels, so every hard-coded size is multiplied by the system DPI factor.
static class DisplayScale
{
    public static float Factor
    {
        get { return NativeMethods.GetDpiForSystem() / 96f; }
    }

    public static int Scale(int pixelsAt96Dpi)
    {
        return (int)Math.Round(pixelsAt96Dpi * Factor);
    }
}

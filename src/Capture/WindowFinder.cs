using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

static class WindowFinder
{
    static readonly string[] DesktopClassNames = { "Progman", "WorkerW" };

    // Screen-coordinate bounds of visible top-level windows, topmost first, so the first hit under the cursor wins.
    public static List<Rectangle> GetVisibleWindowBounds()
    {
        var bounds = new List<Rectangle>();
        uint ownProcessId = (uint)Process.GetCurrentProcess().Id;

        NativeMethods.EnumWindows(delegate(IntPtr windowHandle, IntPtr parameter)
        {
            Rectangle windowBounds;
            if (IsCapturableWindow(windowHandle, ownProcessId) && TryGetVisibleBounds(windowHandle, out windowBounds))
                bounds.Add(windowBounds);
            return true;
        }, IntPtr.Zero);
        return bounds;
    }

    static bool IsCapturableWindow(IntPtr windowHandle, uint ownProcessId)
    {
        if (!NativeMethods.IsWindowVisible(windowHandle) || NativeMethods.IsIconic(windowHandle)) return false;

        uint processId;
        NativeMethods.GetWindowThreadProcessId(windowHandle, out processId);
        if (processId == ownProcessId) return false;

        int cloaked;
        NativeMethods.DwmGetWindowAttribute(windowHandle, NativeMethods.DwmCloaked, out cloaked, sizeof(int));
        return cloaked == 0 && !IsDesktopWindow(windowHandle);
    }

    static bool IsDesktopWindow(IntPtr windowHandle)
    {
        var className = new StringBuilder(64);
        NativeMethods.GetClassName(windowHandle, className, className.Capacity);
        return Array.IndexOf(DesktopClassNames, className.ToString()) >= 0;
    }

    // The DWM frame excludes the invisible resize border that GetWindowRect would include.
    static bool TryGetVisibleBounds(IntPtr windowHandle, out Rectangle bounds)
    {
        NativeMethods.NativeRect frame;
        int result = NativeMethods.DwmGetWindowAttribute(windowHandle, NativeMethods.DwmExtendedFrameBounds, out frame, Marshal.SizeOf(typeof(NativeMethods.NativeRect)));
        bounds = frame.ToRectangle();
        return result == 0 && bounds.Width > 0 && bounds.Height > 0;
    }
}

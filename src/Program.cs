using System;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Per-monitor v2 so capture coordinates are physical pixels on scaled displays.
        NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));

        bool isFirstInstance;
        using (new Mutex(true, AppInfo.Name + ".SingleInstance", out isFirstInstance))
        {
            if (!isFirstInstance) return;
            Application.EnableVisualStyles();
            // Background work (OCR, recording) reports back through this context, so it must exist before the first capture.
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            Application.Run(new TrayApplicationContext());
        }
    }
}

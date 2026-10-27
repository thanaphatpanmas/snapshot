using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

// "Capturing in N…" toast for the timer; click-through so it never gets in the way.
class CountdownForm : NonActivatingPopupForm
{
    const int ToastWidth = 260;
    const int ToastHeight = 56;
    const int ToastTopMargin = 24;
    const int ToastGoneDelayMilliseconds = 150;

    string message = string.Empty;

    CountdownForm()
    {
        BackColor = Color.FromArgb(32, 32, 32);
        ClientSize = new Size(DisplayScale.Scale(ToastWidth), DisplayScale.Scale(ToastHeight));
        Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(workArea.Left + (workArea.Width - Width) / 2, workArea.Top + DisplayScale.Scale(ToastTopMargin));
    }

    protected override bool IsClickThrough { get { return true; } }

    // Blocks while pumping messages, so the screen is only frozen after the toast has really disappeared.
    public static void Run(int seconds)
    {
        using (var toast = new CountdownForm())
        {
            toast.Show();
            for (int remaining = seconds; remaining > 0; remaining--)
            {
                toast.message = string.Format(Loc.T("countdown"), remaining);
                toast.Invalidate();
                PumpMessagesFor(1000);
            }
        }
        PumpMessagesFor(ToastGoneDelayMilliseconds);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using (var font = new Font("Segoe UI", 14f))
            TextRenderer.DrawText(e.Graphics, message, font, ClientRectangle, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    static void PumpMessagesFor(int milliseconds)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(15);
        }
    }
}

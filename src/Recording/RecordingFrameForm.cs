using System.Drawing;
using System.Windows.Forms;

// Red outline drawn just outside the recorded region, so it never appears in the recording itself.
class RecordingFrameForm : NonActivatingPopupForm
{
    const int OutlineThickness = 3;

    readonly int thickness = DisplayScale.Scale(OutlineThickness);

    public RecordingFrameForm(Rectangle recordedRegion)
    {
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        Bounds = Rectangle.Inflate(recordedRegion, thickness, thickness);
    }

    protected override bool IsClickThrough { get { return true; } }

    protected override void OnPaint(PaintEventArgs e)
    {
        using (var outline = new Pen(Color.Red, thickness * 2))
            e.Graphics.DrawRectangle(outline, 0, 0, Width - 1, Height - 1);
    }
}

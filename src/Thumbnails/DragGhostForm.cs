using System.Drawing;
using System.Windows.Forms;

// The picture that follows the cursor while a thumbnail is dragged out.
class DragGhostForm : NonActivatingPopupForm
{
    const double GhostOpacity = 0.9;

    readonly Bitmap image;

    public DragGhostForm(Bitmap image, Point location)
    {
        this.image = image;
        AllowTransparency = true;
        Opacity = GhostOpacity;
        ClientSize = image.Size;
        Location = location;
    }

    protected override bool IsClickThrough { get { return true; } }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImage(image, ClientRectangle);
    }
}

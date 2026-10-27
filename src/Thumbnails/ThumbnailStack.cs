using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// Newest thumbnail sits in the bottom-right corner; older ones are stacked above it.
class ThumbnailStack
{
    const int MaxThumbnails = 5;
    const int ScreenMargin = 16;
    const int Gap = 8;

    readonly List<ThumbnailForm> thumbnails = new List<ThumbnailForm>();

    public void Add(ThumbnailForm thumbnail)
    {
        thumbnails.Insert(0, thumbnail);
        thumbnail.FormClosed += delegate
        {
            thumbnails.Remove(thumbnail);
            Relayout();
        };
        while (thumbnails.Count > MaxThumbnails)
            thumbnails[thumbnails.Count - 1].Close();

        Relayout();
        thumbnail.StartOffscreenRight();
        thumbnail.Show();
    }

    public void CloseAll()
    {
        foreach (ThumbnailForm thumbnail in thumbnails.ToArray())
            thumbnail.Close();
    }

    void Relayout()
    {
        Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
        int margin = DisplayScale.Scale(ScreenMargin);
        int gap = DisplayScale.Scale(Gap);
        int nextBottom = workArea.Bottom - margin;

        foreach (ThumbnailForm thumbnail in thumbnails)
        {
            thumbnail.MoveTo(new Point(workArea.Right - thumbnail.Width - margin, nextBottom - thumbnail.Height));
            nextBottom -= thumbnail.Height + gap;
        }
    }
}

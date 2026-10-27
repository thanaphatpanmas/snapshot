using System.Collections.Specialized;
using System.Drawing;
using System.Windows.Forms;

// Retries because another program may briefly hold the clipboard open.
static class ClipboardHelper
{
    const int RetryCount = 5;
    const int RetryDelayMilliseconds = 100;

    public static void SetImage(Image image)
    {
        var data = new DataObject();
        data.SetImage(image);
        Clipboard.SetDataObject(data, true, RetryCount, RetryDelayMilliseconds);
    }

    public static void SetText(string text)
    {
        Clipboard.SetDataObject(text, true, RetryCount, RetryDelayMilliseconds);
    }

    public static void SetFile(string path)
    {
        var data = new DataObject();
        var files = new StringCollection();
        files.Add(path);
        data.SetFileDropList(files);
        Clipboard.SetDataObject(data, true, RetryCount, RetryDelayMilliseconds);
    }
}

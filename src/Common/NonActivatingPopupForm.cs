using System.Windows.Forms;

class NonActivatingPopupForm : Form
{
    const int WS_EX_NOACTIVATE = 0x08000000;
    const int WS_EX_TOOLWINDOW = 0x00000080;
    const int WS_EX_TRANSPARENT = 0x00000020;

    protected NonActivatingPopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
    }

    // Lets the mouse fall through to whatever is underneath, e.g. a drop target.
    protected virtual bool IsClickThrough { get { return false; } }

    // Keeps the popup from stealing focus from the app being captured.
    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            if (IsClickThrough) parameters.ExStyle |= WS_EX_TRANSPARENT;
            return parameters;
        }
    }
}

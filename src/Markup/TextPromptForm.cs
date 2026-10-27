using System.Drawing;
using System.Windows.Forms;

class TextPromptForm : Form
{
    readonly TextBox textBox = new TextBox { Location = new Point(10, 10), Size = new Size(300, 26) };

    public string EnteredText { get { return textBox.Text; } }

    public TextPromptForm()
    {
        Text = Loc.T("markup.textPrompt");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        ClientSize = new Size(320, 78);

        var okButton = new Button { Text = Loc.T("common.ok"), DialogResult = DialogResult.OK, Location = new Point(150, 44), Size = new Size(76, 26) };
        var cancelButton = new Button { Text = Loc.T("common.cancel"), DialogResult = DialogResult.Cancel, Location = new Point(234, 44), Size = new Size(76, 26) };
        Controls.Add(textBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}

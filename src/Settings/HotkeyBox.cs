using System.Windows.Forms;

// Read-only box that records the shortcut the user presses instead of typing characters.
class HotkeyBox : TextBox
{
    Hotkey hotkey;

    public Hotkey Hotkey
    {
        get { return hotkey; }
        set
        {
            hotkey = value;
            Text = value == null ? string.Empty : value.ToString();
        }
    }

    public HotkeyBox()
    {
        ReadOnly = true;
        ShortcutsEnabled = false;
        BackColor = System.Drawing.SystemColors.Window;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
        if (Hotkey.IsModifierKey(e.KeyCode)) return;

        HotkeyModifiers modifiers = HotkeyModifiers.None;
        if (e.Control) modifiers |= HotkeyModifiers.Control;
        if (e.Alt) modifiers |= HotkeyModifiers.Alt;
        if (e.Shift) modifiers |= HotkeyModifiers.Shift;
        if (modifiers == HotkeyModifiers.None) return;

        Hotkey = new Hotkey(modifiers, e.KeyCode);
    }
}

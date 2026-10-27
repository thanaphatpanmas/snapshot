using System;
using System.Collections.Generic;
using System.Windows.Forms;

class HotkeyWindow : NativeWindow, IDisposable
{
    const int WM_HOTKEY = 0x0312;
    const uint MOD_NOREPEAT = 0x4000;

    readonly Dictionary<int, Action> actionsById = new Dictionary<int, Action>();
    int nextId = 1;

    public HotkeyWindow()
    {
        CreateHandle(new CreateParams());
    }

    // False when another program already owns the combination.
    public bool Register(Hotkey hotkey, Action action)
    {
        int id = nextId++;
        if (!NativeMethods.RegisterHotKey(Handle, id, (uint)hotkey.Modifiers | MOD_NOREPEAT, (uint)hotkey.Key)) return false;
        actionsById[id] = action;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (int id in actionsById.Keys)
            NativeMethods.UnregisterHotKey(Handle, id);
        actionsById.Clear();
    }

    protected override void WndProc(ref Message message)
    {
        Action action;
        if (message.Msg == WM_HOTKEY && actionsById.TryGetValue((int)message.WParam, out action))
        {
            action();
            return;
        }
        base.WndProc(ref message);
    }

    public void Dispose()
    {
        UnregisterAll();
        DestroyHandle();
    }
}

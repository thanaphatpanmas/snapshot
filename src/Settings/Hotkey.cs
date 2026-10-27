using System;
using System.Collections.Generic;
using System.Windows.Forms;

[Flags]
enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4
}

class Hotkey : IEquatable<Hotkey>
{
    public readonly HotkeyModifiers Modifiers;
    public readonly Keys Key;

    public Hotkey(HotkeyModifiers modifiers, Keys key)
    {
        Modifiers = modifiers;
        Key = key;
    }

    // Returns null for anything that is not "<modifiers>+<key>"; a bare key would hijack normal typing.
    public static Hotkey Parse(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        string[] parts = text.Split('+');
        HotkeyModifiers modifiers = HotkeyModifiers.None;
        for (int index = 0; index < parts.Length - 1; index++)
        {
            HotkeyModifiers modifier;
            if (!TryParseModifier(parts[index].Trim(), out modifier)) return null;
            modifiers |= modifier;
        }

        Keys key;
        if (modifiers == HotkeyModifiers.None || !TryParseKey(parts[parts.Length - 1].Trim(), out key)) return null;
        return new Hotkey(modifiers, key);
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & HotkeyModifiers.Control) != 0) parts.Add("Ctrl");
        if ((Modifiers & HotkeyModifiers.Alt) != 0) parts.Add("Alt");
        if ((Modifiers & HotkeyModifiers.Shift) != 0) parts.Add("Shift");
        parts.Add(KeyToText(Key));
        return string.Join("+", parts.ToArray());
    }

    public bool Equals(Hotkey other)
    {
        return other != null && other.Modifiers == Modifiers && other.Key == Key;
    }

    public override bool Equals(object other)
    {
        return Equals(other as Hotkey);
    }

    public override int GetHashCode()
    {
        return ((int)Modifiers * 397) ^ (int)Key;
    }

    static bool TryParseModifier(string text, out HotkeyModifiers modifier)
    {
        switch (text.ToLowerInvariant())
        {
            case "ctrl":
            case "control":
                modifier = HotkeyModifiers.Control;
                return true;
            case "alt":
                modifier = HotkeyModifiers.Alt;
                return true;
            case "shift":
                modifier = HotkeyModifiers.Shift;
                return true;
        }
        modifier = HotkeyModifiers.None;
        return false;
    }

    static bool TryParseKey(string text, out Keys key)
    {
        key = Keys.None;
        if (text.Length == 1 && char.IsDigit(text[0]))
        {
            key = (Keys)((int)Keys.D0 + (text[0] - '0'));
            return true;
        }
        bool isPlainKey = Enum.TryParse(text, true, out key) && key != Keys.None && (key & Keys.Modifiers) == Keys.None;
        return isPlainKey && !IsModifierKey(key);
    }

    static string KeyToText(Keys key)
    {
        bool isDigit = key >= Keys.D0 && key <= Keys.D9;
        return isDigit ? ((int)key - (int)Keys.D0).ToString() : key.ToString();
    }

    public static bool IsModifierKey(Keys key)
    {
        switch (key)
        {
            case Keys.ControlKey:
            case Keys.LControlKey:
            case Keys.RControlKey:
            case Keys.ShiftKey:
            case Keys.LShiftKey:
            case Keys.RShiftKey:
            case Keys.Menu:
            case Keys.LMenu:
            case Keys.RMenu:
            case Keys.LWin:
            case Keys.RWin:
                return true;
        }
        return false;
    }
}

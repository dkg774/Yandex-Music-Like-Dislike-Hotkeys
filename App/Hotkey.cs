using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace YandexMusicLikeDislikeHotkeys
{
    internal sealed class Hotkey : IEquatable<Hotkey>
    {
        public Keys Key { get; private set; }
        public Keys Modifiers { get; private set; }

        public Hotkey(Keys key, Keys modifiers)
        {
            Key = key & Keys.KeyCode;
            Modifiers = modifiers & (Keys.Control | Keys.Alt | Keys.Shift);
        }

        public override string ToString()
        {
            var parts = new List<string>();
            if ((Modifiers & Keys.Control) != 0) parts.Add("Ctrl");
            if ((Modifiers & Keys.Alt) != 0) parts.Add("Alt");
            if ((Modifiers & Keys.Shift) != 0) parts.Add("Shift");
            string keyName = Key.ToString();
            if ((int)Key == (int)Keys.PageUp) keyName = "PageUp";
            else if ((int)Key == (int)Keys.PageDown) keyName = "PageDown";
            parts.Add(keyName);
            return string.Join("+", parts);
        }

        public string ToDisplayString()
        {
            return ToString().Replace("+", " + ");
        }

        public bool Equals(Hotkey other)
        {
            return other != null && Key == other.Key && Modifiers == other.Modifiers;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Hotkey);
        }

        public override int GetHashCode()
        {
            return ((int)Key * 397) ^ (int)Modifiers;
        }

        public static bool TryParse(string text, out Hotkey hotkey)
        {
            hotkey = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string[] parts = text.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            Keys modifiers = Keys.None;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                string part = parts[i].Trim();
                if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                    modifiers |= Keys.Control;
                else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                    modifiers |= Keys.Alt;
                else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                    modifiers |= Keys.Shift;
                else
                    return false;
            }

            Keys key;
            if (!Enum.TryParse(parts[parts.Length - 1].Trim(), true, out key)) return false;
            key &= Keys.KeyCode;
            if (key == Keys.None || IsModifierKey(key)) return false;

            hotkey = new Hotkey(key, modifiers);
            return true;
        }

        public static bool IsModifierKey(Keys key)
        {
            return key == Keys.ShiftKey || key == Keys.ControlKey || key == Keys.Menu ||
                   key == Keys.LShiftKey || key == Keys.RShiftKey ||
                   key == Keys.LControlKey || key == Keys.RControlKey ||
                   key == Keys.LMenu || key == Keys.RMenu ||
                   key == Keys.LWin || key == Keys.RWin;
        }
    }
}

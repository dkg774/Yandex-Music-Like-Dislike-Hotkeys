using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace YandexMusicLikeDislikeHotkeys
{
    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int WmHotkey = 0x0312;
        private const int LikeHotkeyId = 1;
        private const int DislikeHotkeyId = 2;
        private const int ForceLikeHotkeyId = 3;
        private const int ForceDislikeHotkeyId = 4;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModNoRepeat = 0x4000;

        private readonly Action<string> _onHotkey;
        private readonly bool[] _registered = new bool[4];
        private bool _disposed;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public HotkeyWindow(Action<string> onHotkey)
        {
            _onHotkey = onHotkey;
            var parameters = new CreateParams { Caption = "Yandex-Music-Like-Dislike-HotkeysMessageWindow" };
            CreateHandle(parameters);
        }

        public string Apply(Hotkey like, Hotkey dislike, Hotkey forceLike, Hotkey forceDislike, string language)
        {
            bool english = string.Equals(language, "EN", StringComparison.OrdinalIgnoreCase);
            UnregisterAll();

            var hotkeys = new[] { like, dislike, forceLike, forceDislike };
            for (int i = 0; i < hotkeys.Length; i++)
                for (int j = i + 1; j < hotkeys.Length; j++)
                    if (hotkeys[i].Equals(hotkeys[j]))
                        return english ? "All four hotkey combinations must be different." : "Все четыре сочетания хоткеев должны различаться.";

            for (int i = 0; i < hotkeys.Length; i++)
            {
                if (!Register(i + 1, hotkeys[i]))
                {
                    int error = Marshal.GetLastWin32Error();
                    UnregisterAll();
                    string action = i == 0 ? (english ? "like" : "лайка") :
                                    i == 1 ? (english ? "dislike" : "дизлайка") :
                                    i == 2 ? (english ? "force-like" : "принудительного лайка") :
                                             (english ? "force-dislike" : "принудительного дизлайка");
                    return ErrorMessage(action, error, english);
                }
                _registered[i] = true;
            }
            return null;
        }

        private bool Register(int id, Hotkey hotkey)
        {
            uint modifiers = ModNoRepeat;
            if ((hotkey.Modifiers & Keys.Control) != 0) modifiers |= ModControl;
            if ((hotkey.Modifiers & Keys.Alt) != 0) modifiers |= ModAlt;
            if ((hotkey.Modifiers & Keys.Shift) != 0) modifiers |= ModShift;
            return RegisterHotKey(Handle, id, modifiers, (uint)hotkey.Key);
        }

        private static string ErrorMessage(string action, int error, bool english)
        {
            if (english)
                return "Unable to register the " + action + " hotkey (Windows error " + error + "). This key combination may already be used by another application.";
            return "Не удалось зарегистрировать хоткей " + action +
                   " (код Windows " + error + "). Возможно, это сочетание уже занято другой программой.";
        }

        public void UnregisterAll()
        {
            for (int i = 0; i < _registered.Length; i++)
            {
                if (!_registered[i]) continue;
                UnregisterHotKey(Handle, i + 1);
                _registered[i] = false;
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmHotkey)
            {
                int id = message.WParam.ToInt32();
                if (id == LikeHotkeyId) _onHotkey("like");
                else if (id == DislikeHotkeyId) _onHotkey("dislike");
                else if (id == ForceLikeHotkeyId) _onHotkey("force_like");
                else if (id == ForceDislikeHotkeyId) _onHotkey("force_dislike");
            }
            base.WndProc(ref message);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            UnregisterAll();
            DestroyHandle();
        }
    }
}

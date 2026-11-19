using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace YandexMusicLikeDislikeHotkeys
{
    internal sealed class AppSettings
    {
        public const int DefaultPort = 17863;
        // The initial value must also be present in the installed userscript.
        public const string DefaultToken = "ymh-5f8c7b2d-41a9-4d86-92e4-6c1b0a73e5d2";

        public Hotkey LikeHotkey { get; set; }
        public Hotkey DislikeHotkey { get; set; }
        public Hotkey ForceLikeHotkey { get; set; }
        public Hotkey ForceDislikeHotkey { get; set; }
        public int Port { get; private set; }
        public string Token { get; set; }
        public bool LoggingEnabled { get; set; }
        public bool NotificationsEnabled { get; set; }
        public string Language { get; set; }
        public bool LikeSoundEnabled { get; set; }
        public bool DislikeSoundEnabled { get; set; }
        public string LikeSoundPath { get; set; }
        public string DislikeSoundPath { get; set; }
        public bool LikeSoundFileExists { get; private set; }
        public bool DislikeSoundFileExists { get; private set; }
        public string FilePath { get; private set; }

        private AppSettings(string filePath)
        {
            FilePath = filePath;
            LikeHotkey = new Hotkey(Keys.PageUp, Keys.None);
            DislikeHotkey = new Hotkey(Keys.PageDown, Keys.None);
            ForceLikeHotkey = new Hotkey(Keys.PageUp, Keys.Control);
            ForceDislikeHotkey = new Hotkey(Keys.PageDown, Keys.Control);
            Port = DefaultPort;
            Token = DefaultToken;
            LoggingEnabled = false;
            NotificationsEnabled = true;
            Language = "EN";
            LikeSoundEnabled = false;
            DislikeSoundEnabled = false;
            LikeSoundPath = string.Empty;
            DislikeSoundPath = string.Empty;
        }

        public static AppSettings Load()
        {
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            string path = Path.Combine(directory, "settings.ini");
            var settings = new AppSettings(path);

            if (!File.Exists(path))
            {
                settings.ValidateSoundFiles();
                settings.Save();
                return settings;
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("["))
                    continue;

                int separator = line.IndexOf('=');
                if (separator <= 0) continue;
                values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }

            Hotkey parsed;
            string valueText;
            if (values.TryGetValue("LikeHotkey", out valueText) && Hotkey.TryParse(valueText, out parsed))
                settings.LikeHotkey = parsed;
            if (values.TryGetValue("DislikeHotkey", out valueText) && Hotkey.TryParse(valueText, out parsed))
                settings.DislikeHotkey = parsed;
            if (values.TryGetValue("ForceLikeHotkey", out valueText) && Hotkey.TryParse(valueText, out parsed))
                settings.ForceLikeHotkey = parsed;
            if (values.TryGetValue("ForceDislikeHotkey", out valueText) && Hotkey.TryParse(valueText, out parsed))
                settings.ForceDislikeHotkey = parsed;

            int port;
            if (values.TryGetValue("Port", out valueText) && int.TryParse(valueText, out port) && port > 0 && port <= 65535)
                settings.Port = port;
            if (values.TryGetValue("Token", out valueText) && !string.IsNullOrWhiteSpace(valueText))
                settings.Token = valueText;

            bool boolValue;
            if (values.TryGetValue("LoggingEnabled", out valueText) && bool.TryParse(valueText, out boolValue))
                settings.LoggingEnabled = boolValue;
            if (values.TryGetValue("NotificationsEnabled", out valueText) && bool.TryParse(valueText, out boolValue))
                settings.NotificationsEnabled = boolValue;
            if (values.TryGetValue("LikeSoundEnabled", out valueText) && bool.TryParse(valueText, out boolValue))
                settings.LikeSoundEnabled = boolValue;
            if (values.TryGetValue("DislikeSoundEnabled", out valueText) && bool.TryParse(valueText, out boolValue))
                settings.DislikeSoundEnabled = boolValue;
            if (values.TryGetValue("Language", out valueText))
                settings.Language = valueText.Equals("EN", StringComparison.OrdinalIgnoreCase) ? "EN" : "RU";

            if (values.TryGetValue("LikeSoundPath", out valueText))
                settings.LikeSoundPath = valueText;
            if (values.TryGetValue("DislikeSoundPath", out valueText))
                settings.DislikeSoundPath = valueText;

            if (!HasUniqueHotkeys(settings))
            {
                settings.LikeHotkey = new Hotkey(Keys.PageUp, Keys.None);
                settings.DislikeHotkey = new Hotkey(Keys.PageDown, Keys.None);
                settings.ForceLikeHotkey = new Hotkey(Keys.PageUp, Keys.Control);
                settings.ForceDislikeHotkey = new Hotkey(Keys.PageDown, Keys.Control);
            }

            settings.ValidateSoundFiles();
            return settings;
        }

        public void ValidateSoundFiles()
        {
            LikeSoundFileExists = !string.IsNullOrWhiteSpace(LikeSoundPath) && File.Exists(LikeSoundPath);
            DislikeSoundFileExists = !string.IsNullOrWhiteSpace(DislikeSoundPath) && File.Exists(DislikeSoundPath);
        }

        public static bool HasUniqueHotkeys(AppSettings settings)
        {
            var hotkeys = new[]
            {
                settings.LikeHotkey,
                settings.DislikeHotkey,
                settings.ForceLikeHotkey,
                settings.ForceDislikeHotkey
            };
            for (int i = 0; i < hotkeys.Length; i++)
                for (int j = i + 1; j < hotkeys.Length; j++)
                    if (hotkeys[i].Equals(hotkeys[j])) return false;
            return true;
        }

        public void Save()
        {
            string[] lines =
            {
                "; Yandex Music Like/Dislike Hotkeys settings",
                "; Hotkeys accept a key or Ctrl/Alt/Shift combinations, e.g. Ctrl+Shift+K.",
                "[Hotkeys]",
                "LikeHotkey=" + LikeHotkey,
                "DislikeHotkey=" + DislikeHotkey,
                "ForceLikeHotkey=" + ForceLikeHotkey,
                "ForceDislikeHotkey=" + ForceDislikeHotkey,
                "",
                "[Server]",
                "Port=" + Port,
                "Token=" + Token,
                "",
                "[General]",
                "LoggingEnabled=" + LoggingEnabled,
                "NotificationsEnabled=" + NotificationsEnabled,
                "Language=" + Language,
                "",
                "[Sound]",
                "LikeSoundEnabled=" + LikeSoundEnabled,
                "LikeSoundPath=" + (LikeSoundPath ?? string.Empty),
                "DislikeSoundEnabled=" + DislikeSoundEnabled,
                "DislikeSoundPath=" + (DislikeSoundPath ?? string.Empty)
            };
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllLines(FilePath, lines, new UTF8Encoding(false));
        }

        public static string CreateNewToken()
        {
            return "ymh-" + Guid.NewGuid().ToString("N");
        }
    }
}

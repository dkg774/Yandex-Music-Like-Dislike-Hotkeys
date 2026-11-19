using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace YandexMusicLikeDislikeHotkeys
{
    /// <summary>
    /// Thread-safe rolling logger. Both files are stored in the current user's temp folder.
    /// Log messages use English labels and fixed English diagnostic text.
    /// </summary>
    internal static class AppLogger
    {
        private const long MaximumFileSize = 5L * 1024L * 1024L;
        private static readonly object Sync = new object();
        private static bool _enabled;

        public static string CurrentLogPath
        {
            get { return Path.Combine(Path.GetTempPath(), "Yandex-Music-Like-Dislike-Hotkeys.log"); }
        }

        public static string PreviousLogPath
        {
            get { return Path.Combine(Path.GetTempPath(), "Yandex-Music-Like-Dislike-Hotkeys.previous.log"); }
        }

        public static bool Enabled
        {
            get { lock (Sync) return _enabled; }
        }

        public static bool CurrentLogExists
        {
            get { return File.Exists(CurrentLogPath); }
        }

        public static void SetEnabled(bool enabled)
        {
            lock (Sync) _enabled = enabled;
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        private static void Write(string level, string message)
        {
            lock (Sync)
            {
                if (!_enabled) return;

                try
                {
                    string safeMessage = (message ?? string.Empty)
                        .Replace("\r", " ")
                        .Replace("\n", " ")
                        .Trim();
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                                  " [" + level + "] " + safeMessage + Environment.NewLine;
                    byte[] data = new UTF8Encoding(false).GetBytes(line);

                    string current = CurrentLogPath;
                    long currentLength = File.Exists(current) ? new FileInfo(current).Length : 0L;
                    if (currentLength > 0 && currentLength + data.Length >= MaximumFileSize)
                        RotateFiles();

                    using (var stream = new FileStream(current, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    {
                        stream.Write(data, 0, data.Length);
                        stream.Flush();
                    }
                }
                catch
                {
                    // Logging must never terminate or interrupt the application.
                }
            }
        }

        private static void RotateFiles()
        {
            string current = CurrentLogPath;
            string previous = PreviousLogPath;

            if (File.Exists(previous)) File.Delete(previous);
            if (File.Exists(current)) File.Move(current, previous);
        }
    }
}

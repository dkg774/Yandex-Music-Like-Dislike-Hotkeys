using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace YandexMusicLikeDislikeHotkeys
{
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly AppSettings _settings;
        private readonly NotifyIcon _trayIcon;
        private readonly ToolStripMenuItem _startItem;
        private readonly ToolStripMenuItem _stopItem;
        private readonly ToolStripMenuItem _settingsItem;
        private readonly ToolStripMenuItem _exitItem;
        private readonly HotkeyWindow _hotkeyWindow;
        private readonly LocalCommandServer _server;
        private readonly SoundPlayback _soundPlayback;
        private HotkeySettingsForm _settingsForm;
        private bool _running;
        private bool _exiting;

        public TrayApplicationContext()
        {
            _settings = AppSettings.Load();
            AppLogger.SetEnabled(_settings.LoggingEnabled);
            AppLogger.Info("Application started.");
            // Validate the configured audio paths at startup. Missing files are intentionally not logged.
            _settings.ValidateSoundFiles();
            _soundPlayback = new SoundPlayback();

            _server = new LocalCommandServer(_settings.Port, _settings.Token, OnActionSucceeded);
            _hotkeyWindow = new HotkeyWindow(OnHotkeyPressed);

            var menu = new ContextMenuStrip();
            _startItem = new ToolStripMenuItem();
            _startItem.Click += (sender, args) => Start();
            _stopItem = new ToolStripMenuItem();
            _stopItem.Click += (sender, args) => Stop();
            _settingsItem = new ToolStripMenuItem();
            _settingsItem.Click += (sender, args) => ShowSettings();
            _exitItem = new ToolStripMenuItem();
            _exitItem.Click += (sender, args) => ExitApplication();
            menu.Items.Add(_startItem);
            menu.Items.Add(_stopItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_settingsItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_exitItem);

            Icon icon = SystemIcons.Application;
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                try { icon = new Icon(iconPath); } catch { }
            }

            _trayIcon = new NotifyIcon
            {
                Icon = icon,
                ContextMenuStrip = menu,
                Visible = true
            };
            _trayIcon.DoubleClick += (sender, args) => ShowSettings();

            UpdateMenuText();
            Start();
        }

        private string T(string russian, string english)
        {
            return _settings.Language == "EN" ? english : russian;
        }

        private void Start()
        {
            if (_running || _exiting) return;
            try
            {
                _server.Start();
            }
            catch (Exception exception)
            {
                AppLogger.Error("Failed to start local server: " + exception.GetType().Name + ".");
                MessageBox.Show(
                    T("Не удалось запустить локальный сервер на 127.0.0.1:", "Unable to start the local server on 127.0.0.1:") +
                    _settings.Port + ".\n\n" + exception.Message,
                    "Yandex Music Like/Dislike Hotkeys", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _running = true;
            string hotkeyError = ApplyCurrentHotkeys();
            UpdateMenuText();
            if (!string.IsNullOrEmpty(hotkeyError))
            {
                AppLogger.Error("One or more global hotkeys could not be registered.");
                ShowBalloon(T("Сервер запущен, но есть проблема с хоткеями", "Server started, but there is a hotkey issue"), hotkeyError);
            }
            else
            {
                AppLogger.Info("Global hotkeys registered.");
                ShowBalloon(T("Приложение запущено", "Application started"),
                    T("Хоткеи лайка, дизлайка и принудительных действий активны.", "Like, dislike and force-action hotkeys are active."));
            }
        }

        private string ApplyCurrentHotkeys()
        {
            return _hotkeyWindow.Apply(
                _settings.LikeHotkey,
                _settings.DislikeHotkey,
                _settings.ForceLikeHotkey,
                _settings.ForceDislikeHotkey,
                _settings.Language);
        }

        private void Stop()
        {
            if (!_running) return;
            _hotkeyWindow.UnregisterAll();
            _server.Stop();
            _running = false;
            UpdateMenuText();
            ShowBalloon(T("Приложение остановлено", "Application stopped"),
                T("Локальный сервер и глобальные хоткеи выключены.", "The local server and global hotkeys are disabled."));
        }

        private void ShowSettings()
        {
            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                if (_settingsForm.WindowState == FormWindowState.Minimized)
                    _settingsForm.WindowState = FormWindowState.Normal;
                _settingsForm.Show();
                _settingsForm.BringToFront();
                _settingsForm.Activate();
                return;
            }

            _settingsForm = new HotkeySettingsForm(_settings, SaveHotkeys, GenerateNewToken,
                SetLoggingEnabled, SetNotificationsEnabled, SetLanguage);
            _settingsForm.FormClosed += (sender, args) => _settingsForm = null;
            _settingsForm.Show();
            _settingsForm.Activate();
        }

        private string SaveHotkeys(Hotkey like, Hotkey dislike, Hotkey forceLike, Hotkey forceDislike)
        {
            var proposed = new[] { like, dislike, forceLike, forceDislike };
            for (int i = 0; i < proposed.Length; i++)
                for (int j = i + 1; j < proposed.Length; j++)
                    if (proposed[i].Equals(proposed[j]))
                        return T("Все четыре сочетания хоткеев должны различаться.", "All four hotkey combinations must be different.");

            _settings.LikeHotkey = like;
            _settings.DislikeHotkey = dislike;
            _settings.ForceLikeHotkey = forceLike;
            _settings.ForceDislikeHotkey = forceDislike;
            _settings.Save();
            AppLogger.Info("Hotkey settings saved.");

            if (_running)
            {
                string error = ApplyCurrentHotkeys();
                if (!string.IsNullOrEmpty(error))
                {
                    AppLogger.Error("Unable to apply updated global hotkeys.");
                    return T("Настройки сохранены, но регистрация не удалась: ", "Settings saved, but hotkey registration failed: ") + error;
                }
            }
            return null;
        }

        private string GenerateNewToken()
        {
            _settings.Token = AppSettings.CreateNewToken();
            _settings.Save();
            _server.UpdateToken(_settings.Token);
            AppLogger.Info("API token regenerated.");
            return _settings.Token;
        }

        private void SetLoggingEnabled(bool enabled)
        {
            if (_settings.LoggingEnabled == enabled) return;
            if (enabled)
            {
                _settings.LoggingEnabled = true;
                _settings.Save();
                AppLogger.SetEnabled(true);
                AppLogger.Info("Logging enabled.");
            }
            else
            {
                AppLogger.Info("Logging disabled.");
                AppLogger.SetEnabled(false);
                _settings.LoggingEnabled = false;
                _settings.Save();
            }
        }

        private void SetNotificationsEnabled(bool enabled)
        {
            _settings.NotificationsEnabled = enabled;
            _settings.Save();
        }

        private void SetLanguage(string language)
        {
            _settings.Language = language == "EN" ? "EN" : "RU";
            _settings.Save();
            UpdateMenuText();
        }

        private void OnHotkeyPressed(string action)
        {
            if (_running) _server.Enqueue(action);
        }

        private void OnActionSucceeded(string action, bool forced)
        {
            bool isLike = string.Equals(action, "like", StringComparison.OrdinalIgnoreCase);
            if (!isLike && !string.Equals(action, "dislike", StringComparison.OrdinalIgnoreCase)) return;
            _soundPlayback.Play(
                isLike,
                isLike ? _settings.LikeSoundEnabled : _settings.DislikeSoundEnabled,
                isLike ? _settings.LikeSoundPath : _settings.DislikeSoundPath);
        }

        private void UpdateMenuText()
        {
            _startItem.Text = T("Запустить", "Start");
            _stopItem.Text = T("Остановить", "Stop");
            _settingsItem.Text = T("Настройки", "Settings");
            _exitItem.Text = T("Выход", "Exit");
            _startItem.Enabled = !_running;
            _stopItem.Enabled = _running;
            _trayIcon.Text = _running
                ? T("Yandex Music Like/Dislike Hotkeys — запущено", "Yandex Music Like/Dislike Hotkeys — running")
                : T("Yandex Music Like/Dislike Hotkeys — остановлено", "Yandex Music Like/Dislike Hotkeys — stopped");
        }

        private void ShowBalloon(string title, string text)
        {
            if (!_settings.NotificationsEnabled) return;
            _trayIcon.BalloonTipTitle = title;
            _trayIcon.BalloonTipText = text;
            _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
            _trayIcon.ShowBalloonTip(2500);
        }

        private void ExitApplication()
        {
            if (_exiting) return;
            _exiting = true;
            Stop();
            AppLogger.Info("Application exiting.");
            _hotkeyWindow.Dispose();
            _server.Dispose();
            _soundPlayback.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            ExitThread();
        }

        protected override void ExitThreadCore()
        {
            if (!_exiting)
            {
                _exiting = true;
                Stop();
                AppLogger.Info("Application exiting.");
                _hotkeyWindow.Dispose();
                _server.Dispose();
                _soundPlayback.Dispose();
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            base.ExitThreadCore();
        }
    }
}

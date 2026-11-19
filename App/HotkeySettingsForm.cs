using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace YandexMusicLikeDislikeHotkeys
{
    internal sealed class HotkeySettingsForm : Form
    {
        private readonly AppSettings _settings;
        private readonly Func<Hotkey, Hotkey, Hotkey, Hotkey, string> _saveHotkeys;
        private readonly Func<string> _generateToken;
        private readonly Action<bool> _setLogging;
        private readonly Action<bool> _setNotifications;
        private readonly Action<string> _setLanguage;

        private readonly Label _title;
        private readonly Button _languageButton;
        private readonly GroupBox _hotkeysGroup;
        private readonly GroupBox _soundGroup;
        private readonly GroupBox _tokenGroup;
        private readonly GroupBox _generalGroup;
        private readonly Label _likeLabel;
        private readonly Label _dislikeLabel;
        private readonly Label _forceLikeLabel;
        private readonly Label _forceDislikeLabel;
        private readonly TextBox _likeValue;
        private readonly TextBox _dislikeValue;
        private readonly TextBox _forceLikeValue;
        private readonly TextBox _forceDislikeValue;
        private readonly Button _likeButton;
        private readonly Button _dislikeButton;
        private readonly Button _forceLikeButton;
        private readonly Button _forceDislikeButton;
        private readonly CheckBox _likeSoundCheck;
        private readonly CheckBox _dislikeSoundCheck;
        private readonly Button _likeSoundBrowseButton;
        private readonly Button _dislikeSoundBrowseButton;
        private readonly Label _likeSoundPathLabel;
        private readonly Label _dislikeSoundPathLabel;
        private readonly Label _tokenLabel;
        private readonly TextBox _tokenValue;
        private readonly Button _generateTokenButton;
        private readonly Button _copyTokenButton;
        private readonly Label _tokenHint;
        private readonly Label _loggingLabel;
        private readonly Button _loggingButton;
        private readonly Button _openLogButton;
        private readonly Label _logPath;
        private readonly Label _notificationsLabel;
        private readonly Button _notificationsButton;
        private readonly Label _status;
        private readonly Button _exitButton;
        private readonly ToolTip _toolTip;
        private readonly Timer _refreshTimer;

        private Hotkey _like;
        private Hotkey _dislike;
        private Hotkey _forceLike;
        private Hotkey _forceDislike;
        private string _captureTarget;

        public HotkeySettingsForm(AppSettings settings,
            Func<Hotkey, Hotkey, Hotkey, Hotkey, string> saveHotkeys,
            Func<string> generateToken,
            Action<bool> setLogging,
            Action<bool> setNotifications,
            Action<string> setLanguage)
        {
            _settings = settings;
            _like = settings.LikeHotkey;
            _dislike = settings.DislikeHotkey;
            _forceLike = settings.ForceLikeHotkey;
            _forceDislike = settings.ForceDislikeHotkey;
            _saveHotkeys = saveHotkeys;
            _generateToken = generateToken;
            _setLogging = setLogging;
            _setNotifications = setNotifications;
            _setLanguage = setLanguage;

            Text = "Yandex Music Like/Dislike Hotkeys";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            KeyPreview = true;
            ClientSize = new Size(640, 700);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.White;

            _title = new Label
            {
                Font = new Font("Segoe UI Semibold", 14F),
                AutoSize = true,
                Location = new Point(20, 13)
            };
            Controls.Add(_title);

            _languageButton = new Button
            {
                Location = new Point(580, 12),
                Size = new Size(40, 27),
                FlatStyle = FlatStyle.System
            };
            _languageButton.Click += (sender, args) =>
            {
                string newLanguage = _settings.Language == "RU" ? "EN" : "RU";
                _setLanguage(newLanguage);
                ApplyLanguage();
            };
            Controls.Add(_languageButton);

            _hotkeysGroup = new GroupBox { Location = new Point(20, 47), Size = new Size(600, 160) };
            Controls.Add(_hotkeysGroup);
            _likeLabel = AddLabel(_hotkeysGroup, 15, 25);
            _likeValue = AddReadOnlyTextBox(_hotkeysGroup, 200, 21, 245, 25);
            _likeButton = AddButton(_hotkeysGroup, 465, 20, 95, 27, () => BeginCapture("like"));
            _dislikeLabel = AddLabel(_hotkeysGroup, 15, 56);
            _dislikeValue = AddReadOnlyTextBox(_hotkeysGroup, 200, 52, 245, 25);
            _dislikeButton = AddButton(_hotkeysGroup, 465, 51, 95, 27, () => BeginCapture("dislike"));
            _forceLikeLabel = AddLabel(_hotkeysGroup, 15, 87);
            _forceLikeValue = AddReadOnlyTextBox(_hotkeysGroup, 200, 83, 245, 25);
            _forceLikeButton = AddButton(_hotkeysGroup, 465, 82, 95, 27, () => BeginCapture("force_like"));
            _forceDislikeLabel = AddLabel(_hotkeysGroup, 15, 118);
            foreach (var hotkeyLabel in new[] { _likeLabel, _dislikeLabel, _forceLikeLabel, _forceDislikeLabel })
            {
                hotkeyLabel.AutoSize = false;
                hotkeyLabel.Size = new Size(175, 25);
                hotkeyLabel.TextAlign = ContentAlignment.MiddleLeft;
            }
            _forceDislikeValue = AddReadOnlyTextBox(_hotkeysGroup, 200, 114, 245, 25);
            _forceDislikeButton = AddButton(_hotkeysGroup, 465, 113, 95, 27, () => BeginCapture("force_dislike"));

            _soundGroup = new GroupBox { Location = new Point(20, 214), Size = new Size(600, 145) };
            Controls.Add(_soundGroup);
            _likeSoundCheck = new CheckBox { Location = new Point(15, 22), Size = new Size(125, 24), Checked = settings.LikeSoundEnabled };
            _soundGroup.Controls.Add(_likeSoundCheck);
            _likeSoundBrowseButton = AddButton(_soundGroup, 150, 20, 120, 27, BrowseLikeSound);
            _likeSoundPathLabel = AddPathLabel(_soundGroup, 15, 48);
            _dislikeSoundCheck = new CheckBox { Location = new Point(15, 83), Size = new Size(125, 24), Checked = settings.DislikeSoundEnabled };
            _soundGroup.Controls.Add(_dislikeSoundCheck);
            _dislikeSoundBrowseButton = AddButton(_soundGroup, 150, 81, 120, 27, BrowseDislikeSound);
            _dislikeSoundPathLabel = AddPathLabel(_soundGroup, 15, 109);
            _likeSoundCheck.CheckedChanged += (sender, args) => SaveSoundSettings();
            _dislikeSoundCheck.CheckedChanged += (sender, args) => SaveSoundSettings();

            _tokenGroup = new GroupBox { Location = new Point(20, 366), Size = new Size(600, 112) };
            Controls.Add(_tokenGroup);
            _tokenLabel = AddLabel(_tokenGroup, 15, 20);
            _tokenValue = AddReadOnlyTextBox(_tokenGroup, 15, 41, 340, 25);
            _generateTokenButton = AddButton(_tokenGroup, 365, 40, 125, 28, GenerateToken);
            _copyTokenButton = AddButton(_tokenGroup, 500, 40, 85, 28, CopyToken);
            _tokenHint = new Label
            {
                AutoSize = false,
                Location = new Point(15, 75),
                Size = new Size(570, 27),
                ForeColor = Color.FromArgb(105, 105, 105)
            };
            _tokenGroup.Controls.Add(_tokenHint);

            _generalGroup = new GroupBox { Location = new Point(20, 485), Size = new Size(600, 132) };
            Controls.Add(_generalGroup);
            _loggingLabel = AddLabel(_generalGroup, 15, 28);
            _loggingButton = AddButton(_generalGroup, 125, 22, 75, 29, ToggleLogging);
            _openLogButton = AddButton(_generalGroup, 210, 22, 100, 29, OpenLog);
            _notificationsLabel = AddLabel(_generalGroup, 335, 28);
            _notificationsButton = AddButton(_generalGroup, 455, 22, 75, 29, ToggleNotifications);
            _logPath = new Label
            {
                AutoSize = false,
                Location = new Point(15, 60),
                Size = new Size(570, 55),
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(90, 90, 90)
            };
            _generalGroup.Controls.Add(_logPath);

            _status = new Label
            {
                AutoSize = false,
                Location = new Point(20, 625),
                Size = new Size(440, 55),
                ForeColor = Color.FromArgb(90, 90, 90)
            };
            Controls.Add(_status);
            _exitButton = AddButton(this, 490, 657, 110, 29, () => Close());
            CancelButton = _exitButton;

            _toolTip = new ToolTip();
            _toolTip.SetToolTip(_tokenValue, "API token used by the Tampermonkey userscript.");
            _toolTip.SetToolTip(_likeSoundPathLabel, settings.LikeSoundPath ?? string.Empty);
            _toolTip.SetToolTip(_dislikeSoundPathLabel, settings.DislikeSoundPath ?? string.Empty);

            _refreshTimer = new Timer { Interval = 1000 };
            _refreshTimer.Tick += (sender, args) => RefreshLogPath();
            _refreshTimer.Start();
            FormClosed += (sender, args) => _refreshTimer.Stop();

            RefreshHotkeyLabels();
            RefreshGeneralSettings();
            ApplyLanguage();
        }

        private Label AddLabel(Control parent, int x, int y)
        {
            var label = new Label
            {
                AutoSize = true,
                Location = new Point(x, y),
                ForeColor = Color.FromArgb(65, 65, 65)
            };
            parent.Controls.Add(label);
            return label;
        }

        private Label AddPathLabel(Control parent, int x, int y)
        {
            var label = new Label
            {
                AutoSize = false,
                Location = new Point(x, y),
                Size = new Size(545, 23),
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(90, 90, 90)
            };
            parent.Controls.Add(label);
            return label;
        }

        private TextBox AddReadOnlyTextBox(Control parent, int x, int y, int width, int height)
        {
            var box = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                ReadOnly = true,
                BackColor = Color.FromArgb(248, 248, 248),
                BorderStyle = BorderStyle.FixedSingle
            };
            parent.Controls.Add(box);
            return box;
        }

        private Button AddButton(Control parent, int x, int y, int width, int height, Action action)
        {
            var button = new Button
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                FlatStyle = FlatStyle.System
            };
            button.Click += (sender, args) => action();
            parent.Controls.Add(button);
            return button;
        }

        private string T(string russian, string english)
        {
            return _settings.Language == "EN" ? english : russian;
        }

        private void ApplyLanguage()
        {
            Text = T("Настройки — Yandex Music Like/Dislike Hotkeys", "Settings — Yandex Music Like/Dislike Hotkeys");
            _title.Text = T("Настройки", "Settings");
            _languageButton.Text = _settings.Language == "RU" ? "EN" : "RU";
            _toolTip.SetToolTip(_tokenValue, T("Токен для доступа скрипта Tampermonkey к приложению.", "Token used by the Tampermonkey userscript to access the app."));
            _toolTip.SetToolTip(_languageButton, T("Переключить язык интерфейса", "Switch interface language"));

            _hotkeysGroup.Text = T("Глобальные хоткеи", "Global hotkeys");
            _likeLabel.Text = T("Лайк", "Like");
            _dislikeLabel.Text = T("Дизлайк", "Dislike");
            _forceLikeLabel.Text = T("Принудительный лайк", "Force like");
            _forceDislikeLabel.Text = T("Принудительный дизлайк", "Force dislike");
            _likeButton.Text = _dislikeButton.Text = _forceLikeButton.Text = _forceDislikeButton.Text = T("Изменить", "Change");

            _soundGroup.Text = T("Звук", "Sound");
            _likeSoundCheck.Text = T("Звук лайка", "Like sound");
            _dislikeSoundCheck.Text = T("Звук дизлайка", "Dislike sound");
            _likeSoundBrowseButton.Text = _dislikeSoundBrowseButton.Text = T("Выбрать файл", "Choose file");
            _toolTip.SetToolTip(_likeSoundBrowseButton, T("Выбрать WAV-файл для звука лайка.", "Choose a WAV file for the Like sound."));
            _toolTip.SetToolTip(_dislikeSoundBrowseButton, T("Выбрать WAV-файл для звука дизлайка.", "Choose a WAV file for the Dislike sound."));

            _tokenGroup.Text = T("Подключение скрипта", "Userscript connection");
            _tokenLabel.Text = T("Токен приложения", "Application token");
            _generateTokenButton.Text = T("Сгенерировать", "Generate");
            _copyTokenButton.Text = T("Копировать", "Copy");
            _tokenHint.Text = T("После смены токена замените API_TOKEN в скрипте Tampermonkey.",
                                "After changing the token, update API_TOKEN in the Tampermonkey script.");

            _generalGroup.Text = T("Общие настройки", "General settings");
            _openLogButton.Text = T("Открыть лог", "Open log");
            _loggingLabel.Text = T("Логирование", "Logging");
            _notificationsLabel.Text = T("Уведомления", "Notifications");
            _exitButton.Text = T("Выход", "Close");
            if (_captureTarget == null)
                _status.Text = T("Изменения сохраняются автоматически.", "Changes are saved automatically.");
            else
                _status.Text = T("Нажмите нужную клавишу или сочетание (Esc — отмена).", "Press a key or key combination (Esc to cancel).");
            RefreshGeneralSettings();
            RefreshSoundPaths();
        }

        private void BeginCapture(string target)
        {
            _captureTarget = target;
            _status.Text = T("Нажмите нужную клавишу или сочетание (Esc — отмена).",
                             "Press a key or key combination (Esc to cancel).");
            _status.ForeColor = Color.FromArgb(35, 100, 170);
            Activate();
            Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_captureTarget == null) return;

            e.Handled = true;
            e.SuppressKeyPress = true;

            if (e.KeyCode == Keys.Escape)
            {
                _captureTarget = null;
                _status.Text = T("Изменение отменено.", "Change cancelled.");
                _status.ForeColor = Color.FromArgb(90, 90, 90);
                return;
            }

            if (Hotkey.IsModifierKey(e.KeyCode)) return;

            Keys modifiers = Keys.None;
            if (e.Control) modifiers |= Keys.Control;
            if (e.Alt) modifiers |= Keys.Alt;
            if (e.Shift) modifiers |= Keys.Shift;
            var captured = new Hotkey(e.KeyCode, modifiers);

            if (IsAssignedToAnother(captured, _captureTarget))
            {
                _status.Text = T("Этот хоткей уже назначен другой команде.", "This hotkey is already assigned to another action.");
                _status.ForeColor = Color.Firebrick;
                return;
            }

            SetCapturedHotkey(_captureTarget, captured);
            _captureTarget = null;
            string message = _saveHotkeys == null ? null : _saveHotkeys(_like, _dislike, _forceLike, _forceDislike);
            RefreshHotkeyLabels();
            _status.Text = string.IsNullOrEmpty(message)
                ? T("Настройки сохранены автоматически.", "Settings saved automatically.")
                : message;
            _status.ForeColor = string.IsNullOrEmpty(message) ? Color.FromArgb(45, 125, 75) : Color.Firebrick;
        }

        private bool IsAssignedToAnother(Hotkey hotkey, string target)
        {
            return (!target.Equals("like", StringComparison.Ordinal) && hotkey.Equals(_like)) ||
                   (!target.Equals("dislike", StringComparison.Ordinal) && hotkey.Equals(_dislike)) ||
                   (!target.Equals("force_like", StringComparison.Ordinal) && hotkey.Equals(_forceLike)) ||
                   (!target.Equals("force_dislike", StringComparison.Ordinal) && hotkey.Equals(_forceDislike));
        }

        private void SetCapturedHotkey(string target, Hotkey hotkey)
        {
            if (target == "like") _like = hotkey;
            else if (target == "dislike") _dislike = hotkey;
            else if (target == "force_like") _forceLike = hotkey;
            else if (target == "force_dislike") _forceDislike = hotkey;
        }

        private void GenerateToken()
        {
            string token = _generateToken == null ? null : _generateToken();
            if (string.IsNullOrEmpty(token)) return;
            _tokenValue.Text = token;
            SetStatus(T("Новый токен сохранён. Обновите API_TOKEN в Tampermonkey.",
                        "New token saved. Update API_TOKEN in Tampermonkey."), true);
        }

        private void CopyToken()
        {
            try
            {
                Clipboard.SetText(_settings.Token ?? string.Empty);
                SetStatus(T("Токен скопирован в буфер обмена.", "Token copied to the clipboard."), true);
            }
            catch
            {
                SetStatus(T("Не удалось скопировать токен.", "Unable to copy the token."), false);
            }
        }

        private void ToggleLogging()
        {
            _setLogging(!_settings.LoggingEnabled);
            RefreshGeneralSettings();
        }

        private void ToggleNotifications()
        {
            _setNotifications(!_settings.NotificationsEnabled);
            RefreshGeneralSettings();
        }

        private void SaveSoundSettings()
        {
            _settings.LikeSoundEnabled = _likeSoundCheck.Checked;
            _settings.DislikeSoundEnabled = _dislikeSoundCheck.Checked;
            _settings.Save();
            RefreshSoundPaths();
        }

        private void BrowseLikeSound()
        {
            string selected = SelectSoundFile(_settings.LikeSoundPath);
            if (selected == null) return;
            _settings.LikeSoundPath = selected;
            _settings.ValidateSoundFiles();
            _settings.Save();
            RefreshSoundPaths();
            SetStatus(T("Файл звука лайка сохранён.", "Like sound file saved."), true);
        }

        private void BrowseDislikeSound()
        {
            string selected = SelectSoundFile(_settings.DislikeSoundPath);
            if (selected == null) return;
            _settings.DislikeSoundPath = selected;
            _settings.ValidateSoundFiles();
            _settings.Save();
            RefreshSoundPaths();
            SetStatus(T("Файл звука дизлайка сохранён.", "Dislike sound file saved."), true);
        }

        private string SelectSoundFile(string currentPath)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = T("Выберите WAV-файл", "Select a WAV file");
                dialog.Filter = T("Звуковые файлы WAV (*.wav)|*.wav", "WAV sound files (*.wav)|*.wav");
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                dialog.FileName = !string.IsNullOrWhiteSpace(currentPath) && File.Exists(currentPath)
                    ? currentPath
                    : string.Empty;
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
            }
        }

        private void OpenLog()
        {
            string path = AppLogger.CurrentLogPath;
            if (!File.Exists(path))
            {
                RefreshLogPath();
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show(
                    T("Не удалось открыть файл журнала в приложении, связанном с расширением .log.",
                      "Could not open the log file with the application associated with .log files."),
                    "Yandex Music Like/Dislike Hotkeys", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshHotkeyLabels()
        {
            _likeValue.Text = _like.ToDisplayString();
            _dislikeValue.Text = _dislike.ToDisplayString();
            _forceLikeValue.Text = _forceLike.ToDisplayString();
            _forceDislikeValue.Text = _forceDislike.ToDisplayString();
        }

        private void RefreshGeneralSettings()
        {
            _tokenValue.Text = _settings.Token;
            _loggingButton.Text = _settings.Language == "EN"
                ? (_settings.LoggingEnabled ? "ON" : "OFF")
                : (_settings.LoggingEnabled ? "ВКЛ" : "ВЫКЛ");
            _notificationsButton.Text = _settings.Language == "EN"
                ? (_settings.NotificationsEnabled ? "ON" : "OFF")
                : (_settings.NotificationsEnabled ? "ВКЛ" : "ВЫКЛ");
            RefreshLogPath();
        }

        private void RefreshSoundPaths()
        {
            _settings.ValidateSoundFiles();
            _likeSoundCheck.Checked = _settings.LikeSoundEnabled;
            _dislikeSoundCheck.Checked = _settings.DislikeSoundEnabled;
            _likeSoundPathLabel.Text = FormatSoundPath(_settings.LikeSoundPath, _settings.LikeSoundFileExists);
            _dislikeSoundPathLabel.Text = FormatSoundPath(_settings.DislikeSoundPath, _settings.DislikeSoundFileExists);
            _toolTip.SetToolTip(_likeSoundPathLabel, _settings.LikeSoundPath ?? string.Empty);
            _toolTip.SetToolTip(_dislikeSoundPathLabel, _settings.DislikeSoundPath ?? string.Empty);
        }

        private string FormatSoundPath(string path, bool exists)
        {
            if (string.IsNullOrWhiteSpace(path))
                return T("Файл не выбран.", "No file selected.");
            return (exists ? (_settings.Language == "EN" ? "[OK] " : "[Найден] ")
                           : (_settings.Language == "EN" ? "[Not found] " : "[Не найден] ")) + path;
        }

        private void RefreshLogPath()
        {
            string path = AppLogger.CurrentLogPath;
            bool exists = File.Exists(path);
            _openLogButton.Enabled = exists;
            _logPath.Text = exists
                ? T("Файл журнала: ", "Log file: ") + path
                : T("Файл .log ещё не создан.", "The .log file has not been created yet.");
            _toolTip.SetToolTip(_logPath, exists ? path : string.Empty);
        }

        private void SetStatus(string message, bool success)
        {
            _status.Text = message;
            _status.ForeColor = success ? Color.FromArgb(45, 125, 75) : Color.Firebrick;
        }
    }
}

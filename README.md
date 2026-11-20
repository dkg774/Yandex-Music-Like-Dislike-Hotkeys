# Yandex Music Like/Dislike Hotkeys

A **Windows** tray app and **Tampermonkey** userscript that let you like/dislike tracks on https://music.yandex.ru via global hotkeys from any application.

## Components

- `App/` — C# WinForms tray application (`WinExe`, no console window).
- `UserScript/Yandex-Music-Like-Dislike-Hotkeys.user.js` — Tampermonkey userscript.
- `Yandex-Music-Like-Dislike-Hotkeys.sln` — Visual Studio solution.

## Communication

1. The desktop application registers four global hotkeys and runs a local HTTP server bound to `127.0.0.1:17863`.
2. The userscript polls `http://127.0.0.1:17863/api/poll` every **500 ms**.
3. The userscript locates the current Like/Dislike controls and processes normal or forced commands.
4. The userscript posts diagnostic and action events to `http://127.0.0.1:17863/api/log`.
5. Both endpoints require the API token. The server is bound to IPv4 loopback and is not exposed to the local network.

The userscript uses Tampermonkey's `GM_xmlhttpRequest`; `@connect 127.0.0.1` is declared in its metadata.

## Build

1. On Windows 10, open `Yandex-Music-Like-Dislike-Hotkeys.sln` in Visual Studio 2019 or newer.
2. Install the **.NET desktop development** workload and the **.NET Framework 4.8 Developer Pack (or newer)**, if needed.
3. Build the solution using the `Release` configuration.
4. Run `App/bin/Release/Yandex-Music-Like-Dislike-Hotkeys.exe`.

The project uses `OutputType=WinExe`, so it does not create a console window. `Build-Release.bat` can be run from a Visual Studio Developer Command Prompt.

## Install the userscript

1. Install *Tampermonkey* in your browser.
2. Open *Tampermonkey Dashboard* → **Create a new script**.
3. Replace the editor contents with `UserScript/Yandex-Music-Like-Dislike-Hotkeys.user.js` and save.
4. Open or reload `https://music.yandex.ru/`.

The userscript's initial API token matches the desktop application's default token.

### Changing the API token

In the desktop app, open **Settings** and press **Generate**. The new token is saved to `settings.ini` and becomes active in the local server immediately. Press **Copy**, open the Tampermonkey script, replace the value of `API_TOKEN` with the copied token, and save/reload the userscript. The token is deliberately not fetched from an unauthenticated endpoint.

## Tray menu and settings

Right-click the tray icon to open:

- **Start** — start the local server and register global hotkeys.
- **Stop** — stop the server and unregister hotkeys.
- **Settings** — open the settings window.
- **Exit** — close the application.

The settings window includes:

- Four global hotkey editors: Like, Dislike, Force like and Force dislike. A key or a combination with Ctrl/Alt/Shift is supported; Esc cancels capture.
- Separate Like and Dislike sound settings: enable checkbox, WAV file picker and a file status label.
- Current API token, **Generate**, and **Copy** buttons.
- A single toggle button for logging and **Open log**.
- Current log path, or a message that the `.log` file has not yet been created.
- A single toggle button for tray notifications.
- A small **RU/EN** button that changes the tray menu, settings window, sound settings and notifications immediately.

### Default hotkeys

| Action | Default hotkey | Behaviour |
|---|---|---|
| Like | `PageUp` | Click Like only if it is not already selected |
| Dislike | `PageDown` | Click Dislike only if it is not already selected |
| Force like | `Ctrl+PageUp` | Click Like regardless of its current `aria-pressed` state |
| Force dislike | `Ctrl+PageDown` | Click Dislike regardless of its current `aria-pressed` state |

All four combinations must be unique. If Windows cannot register one of them because another application already uses it, choose a different combination in Settings.

## Sound settings

The sound section has independent settings for Like and Dislike. Select a local `.wav` file and enable its checkbox. Sounds are played by the desktop application's own process through .NET Framework's `System.Media.SoundPlayer`; no external player is launched.

A sound is played only after the userscript confirms that the corresponding button click was performed. It is not played when a normal command is skipped because the track already has that rating. Force clicks use the same Like/Dislike sound as their normal counterparts.

The settings window displays `[OK]` / `[Not found]` before the selected path in English, or the localized equivalent in Russian. Missing audio files are not written to the log. Sound is disabled by default, and no sound file is selected initially.

## Rating-state protection

Yandex Music rating controls have three states: liked, disliked, and no rating. Clicking an already selected Like or Dislike button can clear the rating. To avoid this, immediately before every normal click the userscript checks the target button's `aria-pressed` attribute:

- `aria-pressed="true"` on Like: the normal Like command is skipped and `already_liked` is logged.
- `aria-pressed="true"` on Dislike: the normal Dislike command is skipped and `already_disliked` is logged.
- Otherwise, the normal action is clicked.
- Force Like / Force Dislike bypass this guard and always attempt the click.

The userscript checks this attribute again on every retry immediately before a click. The force commands are intentionally explicit overrides and can toggle an already selected rating off.

## Settings file

`settings.ini` is created beside `Yandex-Music-Like-Dislike-Hotkeys.exe`. Example:

```ini
[Hotkeys]
LikeHotkey=PageUp
DislikeHotkey=PageDown
ForceLikeHotkey=Ctrl+PageUp
ForceDislikeHotkey=Ctrl+PageDown

[Server]
Port=17863
Token=ymh-...

[General]
LoggingEnabled=False
NotificationsEnabled=True
Language=EN

[Sound]
LikeSoundEnabled=False
LikeSoundPath=
DislikeSoundEnabled=False
DislikeSoundPath=
```

The application needs write permission to its executable folder.

## Logging

Logging is disabled by default and can be enabled from **Settings**. When enabled, the application writes UTF-8 text logs to the current user's `%TEMP%` directory:

- `%TEMP%\Yandex-Music-Like-Dislike-Hotkeys.log` — current log.
- `%TEMP%\Yandex-Music-Like-Dislike-Hotkeys.previous.log` — previous log after rotation.

Each file is limited to 5 MiB. When the current log reaches the size limit, the older `previous` file is deleted and the current log is renamed to `previous`; a new current log is then created for the next entry. Each line contains a local timestamp in `yyyy-MM-dd HH:mm:ss` format and either `[INFO]` or `[ERROR]`.

Log diagnostic messages are in English. Artist and track names are kept exactly as returned by Yandex Music, so their original spelling/language is preserved.

Logged events include:
- application/server lifecycle;
- HTTP error statuses with their reason names;
- userscript polling failures;
- successful normal clicks;
- successful forced clicks (clearly marked as force-clicked);
- already-liked/already-disliked skips;
- missing track metadata with selectors tried;
- and missing Like/Dislike buttons with selectors tried.
    
Routine successful `200` poll responses are intentionally not logged to prevent a new log entry every 500 ms. Missing sound files are not logged.

## Selector order

### Like/Dislike buttons

The userscript checks these selector pairs in order:

1. `div[class^="PlayerBarDesktopWithBackgroundProgressBar_sonata"] > button[data-test-id*="LIKE_BUTTON"]` and the matching `DISLIKE_BUTTON` selector.
2. `div[class^="VibePlayerBar_progress"] > button[data-test-id*="LIKE_BUTTON"]` and the matching `DISLIKE_BUTTON` selector.

### Track and artist metadata

1. Track: `div[class^="PlayerBarDesktopWithBackgroundProgressBar_description"] a[data-test-id="TRACK_TITLE"] > span`.
2. Artist: `div[class^="PlayerBarDesktopWithBackgroundProgressBar_description"] a[data-test-id="SEPARATED_ARTIST_TITLE"] > span`.
3. Fallback track container: `div[class^="VibePlayerBar_progress"] div[class*="VibePlayerbarTrackName_trackNameText"]`.
4. Fallback artist: `div[class^="VibePlayerBar_progress"] div[class*="VibePlayerbarTrackName_trackNameText"] > span`.

The fallback artist suffix is normalized to `Artist — Track` with spaces around the em dash.

## Notes

- If no track metadata is found, the userscript reports the selectors it tried but still attempts the requested Like/Dislike action.
- Button lookup is retried up to 10 times with a 150 ms delay between attempts.
- Commands older than 3 seconds are discarded by the desktop application so a stale key press is not replayed after a browser reconnect.

## Requirements
- Windows 10+ (x64)
- .Net Framework 4.8
- Tampermonkey:
    - Firefox: https://addons.mozilla.org/en-US/firefox/addon/tampermonkey/
    - Chrome: https://chromewebstore.google.com/detail/tampermonkey/dhdgffkkebhmkfjojejmpbldmpobfkfo

## License
- GNU General Public License v3.0

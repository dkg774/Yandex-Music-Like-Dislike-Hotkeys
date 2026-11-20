# Yandex Music Like/Dislike Hotkeys

A **Windows** tray app and **Tampermonkey** Userscript that let you like/dislike tracks on [Yandex Music](https://music.yandex.ru) via global hotkeys from any app.

## Components

- `App/` — C# WinForms tray application (`WinExe`, no console window).
- `UserScript/Yandex-Music-Like-Dislike-Hotkeys.user.js` — Tampermonkey Userscript.
- `Yandex-Music-Like-Dislike-Hotkeys.sln` — Visual Studio solution.

## Communication

1. The desktop application registers four global hotkeys and runs a local HTTP server bound to `127.0.0.1:17863`.
2. The Userscript polls `http://127.0.0.1:17863/api/poll` every **500 ms**.
3. The Userscript locates the current Like/Dislike controls and processes normal or forced commands.
4. The Userscript posts diagnostic and action events to `http://127.0.0.1:17863/api/log`.
5. Both endpoints require the API token. The server is bound to IPv4 loopback and is not exposed to the local network.

The Userscript uses Tampermonkey's `GM_xmlhttpRequest`; `@connect 127.0.0.1` is declared in its metadata.

## Build

1. On Windows 10, open `Yandex-Music-Like-Dislike-Hotkeys.sln` in Visual Studio 2019 or newer.
2. Install the **.NET desktop development** workload and the **.NET Framework 4.8 Developer Pack (or newer)**, if needed.
3. Build the solution using the `Release` configuration.
4. Run `App/bin/Release/Yandex-Music-Like-Dislike-Hotkeys.exe`.

The project uses `OutputType=WinExe`, so it does not create a console window. `Build-Release.bat` can be run from a *Visual Studio* Developer Command Prompt.

## Install the Userscript

1. Install *Tampermonkey* in your browser.
2. Open *Tampermonkey Dashboard* → **Create a new script**.
3. Replace the editor contents with `UserScript/Yandex-Music-Like-Dislike-Hotkeys.user.js` and save.
4. Open or reload `https://music.yandex.ru/`.

The Userscript's initial API token matches the desktop application's default token.

### Changing the API token

In the desktop app, open **Settings** and press **Generate**. The new token is saved to `settings.ini` and becomes active in the local server immediately. Press **Copy**, open the Tampermonkey script, replace the value of `API_TOKEN` with the copied token, and save/reload the Userscript. The token is deliberately not fetched from an unauthenticated endpoint.

## Tray menu and settings

Right-click the tray icon to open:

- **Start** — start the local server and register global hotkeys.
- **Stop** — stop the server and unregister hotkeys.
- **Settings** — open the settings window.
- **Exit** — close the application.

The `Settings` window includes:

- Four global hotkey editors: Like, Dislike, Force like and Force dislike. A key or a combination with `Ctrl/Alt/Shift` is supported; `Esc` cancels capture.
- Separate **Like** and **Dislike** sound settings: enable checkbox, WAV file picker and a file status label.
- Current API token, **Generate**, and **Copy** buttons.
- A toggle button for logging and **Open log**.
- Current log path, or a message that the `.log` file has not yet been created.
- A toggle button for tray notifications.
- A **EN/RU** button that changes localization.

### Default hotkeys

| Action | Default hotkey | Behaviour |
|---|---|---|
| Like | `PageUp` | Click *Like* only if a track is not already Liked |
| Dislike | `PageDown` | Click *Dislike* only if a track is not already Disliked |
| Force like | `Ctrl+PageUp` | Click *Like* regardless of track's current *Like* state |
| Force dislike | `Ctrl+PageDown` | Click *Dislike* regardless of track's current *Dislike* state |

All four combinations must be unique. If Windows cannot register one of them because another application already uses it, **choose a different combination** in `Settings`.

## Sound settings

The sound section has independent settings for Like and Dislike. Select a local `.wav` file and enable its checkbox. Sounds are played by the desktop application's own process through .NET Framework's `System.Media.SoundPlayer`; no external player is launched.

A sound is played only after the Userscript confirms that the corresponding button click was performed. It is not played when a normal command is skipped because the track already has that rating. Force clicks use the same Like/Dislike sound as their normal counterparts.

The settings window displays `[OK]` / `[Not found]` before the selected path. Missing audio files are not written to the log. Sound is disabled by default, and no sound file is selected initially.

## Rating-state protection

Yandex Music tracks have three states: liked, disliked, and no rating. Clicking a Like/Dislike on already Liked/Disliked track will remove Like/Dislike. To avoid this, immediately before every normal click the Userscript checks the target button's `aria-pressed` attribute:

- `aria-pressed="true"` on Like: the normal Like command is skipped and `already_liked` is logged.
- `aria-pressed="true"` on Dislike: the normal Dislike command is skipped and `already_disliked` is logged.
- Otherwise, the normal action is clicked.
- Force Like / Force Dislike bypass this guard and always attempt the click.

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

Logging is **disabled** by default and can be **enabled** from `Settings`. When enabled, the application writes *UTF-8* text logs to the current user's `%TEMP%` directory:

- `%TEMP%\Yandex-Music-Like-Dislike-Hotkeys.log` — current log.
- `%TEMP%\Yandex-Music-Like-Dislike-Hotkeys.previous.log` — previous log after rotation.

Each file is limited to 5 MiB. When the current log reaches the size limit, the older `previous` file is deleted and the current log is renamed to `previous`; a new current log is then created for the next entry. Each line contains a local timestamp in `yyyy-MM-dd HH:mm:ss` format and either `[INFO]` or `[ERROR]`.

Logged events include:
- application/server lifecycle;
- HTTP error statuses with their reason names;
- Userscript polling failures;
- successful normal clicks;
- successful forced clicks (clearly marked as force-clicked);
- already-liked/already-disliked skips;
- missing track metadata with selectors tried;
- and missing Like/Dislike buttons with selectors tried.
    
Routine successful `200` poll responses are intentionally not logged to prevent a new log entry every 500 ms. Missing sound files are not logged.

## Selector order

### Like/Dislike buttons

The Userscript checks these selector pairs in order:

1. `div[class^="PlayerBarDesktopWithBackgroundProgressBar_sonata"] > button[data-test-id*="LIKE_BUTTON"]`<br>
    and the matching `DISLIKE_BUTTON` selector.
2. `div[class^="VibePlayerBar_progress"] > button[data-test-id*="LIKE_BUTTON"]`<br>
    and the matching `DISLIKE_BUTTON` selector.

### Track and artist metadata

1. **Track:**<br>`div[class^="PlayerBarDesktopWithBackgroundProgressBar_description"] a[data-test-id="TRACK_TITLE"] > span`.
2. **Artist:**<br>`div[class^="PlayerBarDesktopWithBackgroundProgressBar_description"] a[data-test-id="SEPARATED_ARTIST_TITLE"] > span`.
3. **Fallback track container:**<br>`div[class^="VibePlayerBar_progress"] div[class*="VibePlayerbarTrackName_trackNameText"]`.
4. **Fallback artist:**<br>`div[class^="VibePlayerBar_progress"] div[class*="VibePlayerbarTrackName_trackNameText"] > span`.

The fallback artist suffix is normalized to `Artist — Track` with spaces around the em dash.

## Notes

- If no track metadata is found, the Userscript reports the selectors it tried but still attempts the requested Like/Dislike action.
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

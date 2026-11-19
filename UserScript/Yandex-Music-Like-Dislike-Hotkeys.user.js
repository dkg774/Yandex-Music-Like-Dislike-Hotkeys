// ==UserScript==
// @name         Yandex Music Like/Dislike Hotkeys
// @namespace    local.yandex-music-hotkeys
// @version      1.1.1
// @description  Control Yandex Music like and dislike actions with global Windows hotkeys.
// @icon         https://icons.duckduckgo.com/ip2/music.yandex.ru.ico
// @match        https://music.yandex.ru/*
// @run-at       document-idle
// @grant        GM_xmlhttpRequest
// @connect      127.0.0.1
// ==/UserScript==

(function () {
    'use strict';

    const API_BASE_URL = 'http://127.0.0.1:17863/api';
    // After generating a new token in the desktop app, paste it here.
    const API_TOKEN = 'ymh-5f8c7b2d-41a9-4d86-92e4-6c1b0a73e5d2';
    const POLL_INTERVAL_MS = 500;
    const BUTTON_RETRY_COUNT = 10;
    const BUTTON_RETRY_DELAY_MS = 150;

    const BUTTON_SELECTORS = [
        {
            like: 'div[class^="PlayerBarDesktopWithBackgroundProgressBar_sonata"] > button[data-test-id*="LIKE_BUTTON"]',
            dislike: 'div[class^="PlayerBarDesktopWithBackgroundProgressBar_sonata"] > button[data-test-id*="DISLIKE_BUTTON"]'
        },
        {
            like: 'div[class^="VibePlayerBar_progress"] > button[data-test-id*="LIKE_BUTTON"]',
            dislike: 'div[class^="VibePlayerBar_progress"] > button[data-test-id*="DISLIKE_BUTTON"]'
        }
    ];

    const TRACK_SELECTORS = [
        {
            track: 'div[class^="PlayerBarDesktopWithBackgroundProgressBar_description"] a[data-test-id="TRACK_TITLE"] > span',
            artist: 'div[class^="PlayerBarDesktopWithBackgroundProgressBar_description"] a[data-test-id="SEPARATED_ARTIST_TITLE"] > span'
        },
        {
            trackContainer: 'div[class^="VibePlayerBar_progress"] div[class*="VibePlayerbarTrackName_trackNameText"]',
            artist: 'div[class^="VibePlayerBar_progress"] div[class*="VibePlayerbarTrackName_trackNameText"] > span'
        }
    ];

    let polling = false;
    let commandProcessing = Promise.resolve();
    let lastPollFailure = null;
    let telemetryInProgress = false;
    let nextTelemetryAttempt = 0;
    const telemetryQueue = [];

    function delay(milliseconds) {
        return new Promise(function (resolve) {
            window.setTimeout(resolve, milliseconds);
        });
    }

    function cleanText(value) {
        return (value || '').replace(/\s+/g, ' ').trim();
    }

    function getTrackInfo() {
        const primary = TRACK_SELECTORS[0];
        const primaryTrack = document.querySelector(primary.track);
        const primaryArtist = document.querySelector(primary.artist);

        if (primaryTrack && primaryArtist) {
            const track = cleanText(primaryTrack.textContent);
            const artist = cleanText(primaryArtist.textContent);
            if (track && artist) return { artist: artist, track: track };
        }

        const fallback = TRACK_SELECTORS[1];
        const trackContainer = document.querySelector(fallback.trackContainer);
        const artistElement = document.querySelector(fallback.artist);
        if (trackContainer && artistElement) {
            const rawArtist = cleanText(artistElement.textContent);
            const artist = rawArtist.replace(/\s*[—–-]\s*$/, '').trim();
            let track = cleanText(trackContainer.textContent);

            if (rawArtist && track.indexOf(rawArtist) === 0) {
                track = cleanText(track.substring(rawArtist.length));
            }

            if (track && artist) return { artist: artist, track: track };
        }

        return null;
    }

    function getTrackSelectorList() {
        return [
            TRACK_SELECTORS[0].track,
            TRACK_SELECTORS[0].artist,
            TRACK_SELECTORS[1].trackContainer,
            TRACK_SELECTORS[1].artist
        ];
    }

    function findButtons() {
        for (const selectors of BUTTON_SELECTORS) {
            const like = document.querySelector(selectors.like);
            const dislike = document.querySelector(selectors.dislike);
            if (like && dislike) return { like: like, dislike: dislike };
        }
        return null;
    }

    function getButtonSelectorList(action) {
        return BUTTON_SELECTORS.map(function (selectors) { return selectors[action]; });
    }

    function queueTelemetry(eventData) {
        if (telemetryQueue.length < 100) telemetryQueue.push(eventData);
        flushTelemetry();
    }

    function flushTelemetry() {
        if (telemetryInProgress || telemetryQueue.length === 0 || Date.now() < nextTelemetryAttempt) return;

        telemetryInProgress = true;
        GM_xmlhttpRequest({
            method: 'POST',
            url: API_BASE_URL + '/log?token=' + encodeURIComponent(API_TOKEN),
            headers: { 'Content-Type': 'application/json; charset=utf-8' },
            data: JSON.stringify(telemetryQueue[0]),
            timeout: 2500,
            onload: function (response) {
                if (response.status === 202) {
                    telemetryQueue.shift();
                    nextTelemetryAttempt = 0;
                } else {
                    nextTelemetryAttempt = Date.now() + 3000;
                }
                telemetryInProgress = false;
                if (response.status === 202) flushTelemetry();
            },
            onerror: function () {
                telemetryInProgress = false;
                nextTelemetryAttempt = Date.now() + 3000;
            },
            ontimeout: function () {
                telemetryInProgress = false;
                nextTelemetryAttempt = Date.now() + 3000;
            }
        });
    }

    async function executeAction(action, forced) {
        if (action !== 'like' && action !== 'dislike') return;
        forced = forced === true;

        const trackInfo = getTrackInfo();
        if (!trackInfo) {
            queueTelemetry({
                type: 'track_not_found',
                action: action,
                selectors: getTrackSelectorList()
            });
        }

        for (let attempt = 0; attempt < BUTTON_RETRY_COUNT; attempt += 1) {
            const buttons = findButtons();
            const button = buttons ? buttons[action] : null;

            if (button && !button.disabled && button.getAttribute('aria-disabled') !== 'true') {
                // Check the target button immediately before every click. A normal
                // click on an already selected rating would clear that rating.
                if (!forced && button.getAttribute('aria-pressed') === 'true') {
                    queueTelemetry({
                        type: action === 'like' ? 'already_liked' : 'already_disliked',
                        action: action,
                        artist: trackInfo ? trackInfo.artist : '',
                        track: trackInfo ? trackInfo.track : ''
                    });
                    return;
                }

                try {
                    button.click();
                    queueTelemetry({
                        type: forced ? 'force_action_success' : 'action_success',
                        action: action,
                        forced: forced,
                        artist: trackInfo ? trackInfo.artist : '',
                        track: trackInfo ? trackInfo.track : ''
                    });
                    return;
                } catch (error) {
                    queueTelemetry({
                        type: 'button_click_failed',
                        action: action,
                        selectors: getButtonSelectorList(action)
                    });
                    return;
                }
            }

            await delay(BUTTON_RETRY_DELAY_MS);
        }

        queueTelemetry({
            type: 'button_not_found',
            action: action,
            selectors: getButtonSelectorList(action)
        });
    }

    async function processCommands(commands) {
        if (!Array.isArray(commands)) return;
        for (const command of commands) {
            const forced = typeof command === 'string' && command.indexOf('force_') === 0;
            const action = forced ? command.substring('force_'.length) : command;
            await executeAction(action, forced);
        }
    }

    function scheduleNextPoll() {
        window.setTimeout(poll, POLL_INTERVAL_MS);
    }

    function reportPollFailure(reason, eventType, status) {
        const normalizedReason = cleanText(reason) || 'Network error';
        if (lastPollFailure === normalizedReason) return;
        lastPollFailure = normalizedReason;
        if (eventType === 'poll_http_error') {
            queueTelemetry({ type: 'poll_http_error', status: status });
        } else {
            queueTelemetry({ type: 'poll_error', reason: normalizedReason });
        }
    }

    function poll() {
        if (polling) {
            scheduleNextPoll();
            return;
        }

        polling = true;
        const url = API_BASE_URL + '/poll?token=' + encodeURIComponent(API_TOKEN) + '&_=' + Date.now();

        GM_xmlhttpRequest({
            method: 'GET',
            url: url,
            timeout: 2500,
            onload: function (response) {
                if (response.status !== 200) {
                    reportPollFailure('HTTP ' + response.status + ' ' + (response.statusText || 'Unknown status'),
                        'poll_http_error', response.status + ' ' + (response.statusText || 'Unknown status'));
                    polling = false;
                    scheduleNextPoll();
                    return;
                }

                lastPollFailure = null;
                flushTelemetry();
                let commands = [];
                try {
                    const payload = JSON.parse(response.responseText);
                    commands = payload.commands || [];
                } catch (error) {
                    reportPollFailure('Invalid JSON response from local application');
                }

                polling = false;
                scheduleNextPoll();
                commandProcessing = commandProcessing
                    .then(function () { return processCommands(commands); })
                    .catch(function () {
                        queueTelemetry({ type: 'poll_error', reason: 'Command processing failed' });
                    });
            },
            onerror: function () {
                reportPollFailure('Network error while polling local application');
                polling = false;
                scheduleNextPoll();
            },
            ontimeout: function () {
                reportPollFailure('Polling request timed out');
                polling = false;
                scheduleNextPoll();
            }
        });
    }

    poll();
}());

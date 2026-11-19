using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace YandexMusicLikeDislikeHotkeys
{
    /// <summary>
    /// Small HTTP server bound only to IPv4 loopback. The userscript polls /api/poll
    /// and posts diagnostic events to /api/log.
    /// </summary>
    internal sealed class LocalCommandServer : IDisposable
    {
        private const int MaximumHeaderBytes = 16 * 1024;
        private const int MaximumBodyBytes = 64 * 1024;
        private readonly int _port;
        private readonly Action<string, bool> _onActionSucceeded;
        private volatile string _token;

        private sealed class PendingCommand
        {
            public string Action { get; set; }
            public DateTime CreatedUtc { get; set; }
        }

        private sealed class HttpRequestData
        {
            public string RequestLine { get; set; }
            public string Body { get; set; }
        }

        private readonly ConcurrentQueue<PendingCommand> _commands = new ConcurrentQueue<PendingCommand>();
        private readonly ConcurrentDictionary<string, DateTime> _lastHttpErrorLog = new ConcurrentDictionary<string, DateTime>();
        private readonly object _queueLock = new object();
        private TcpListener _listener;
        private CancellationTokenSource _cancellation;
        private Task _acceptTask;
        private bool _disposed;

        public LocalCommandServer(int port, string token, Action<string, bool> onActionSucceeded)
        {
            _port = port;
            _token = token;
            _onActionSucceeded = onActionSucceeded;
        }

        public bool IsRunning { get; private set; }

        public void UpdateToken(string token)
        {
            if (!string.IsNullOrWhiteSpace(token)) _token = token;
        }

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException("LocalCommandServer");
            if (IsRunning) return;

            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start(16);
            _cancellation = new CancellationTokenSource();
            IsRunning = true;
            CancellationToken token = _cancellation.Token;
            TcpListener listener = _listener;
            _acceptTask = Task.Run(() => AcceptLoopAsync(listener, token));
            AppLogger.Info("Local HTTP server started on 127.0.0.1:" + _port + ".");
        }

        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            try { _cancellation.Cancel(); } catch { }
            try { _listener.Stop(); } catch { }
            try { _cancellation.Dispose(); } catch { }
            _listener = null;
            _cancellation = null;
            lock (_queueLock)
            {
                PendingCommand ignored;
                while (_commands.TryDequeue(out ignored)) { }
            }
            AppLogger.Info("Local HTTP server stopped.");
        }

        public void Enqueue(string action)
        {
            if (!IsRunning || (action != "like" && action != "dislike" &&
                               action != "force_like" && action != "force_dislike")) return;
            lock (_queueLock)
            {
                while (_commands.Count >= 64)
                {
                    PendingCommand ignored;
                    _commands.TryDequeue(out ignored);
                }
                _commands.Enqueue(new PendingCommand { Action = action, CreatedUtc = DateTime.UtcNow });
            }
        }

        private async Task AcceptLoopAsync(TcpListener listener, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client = null;
                try
                {
                    client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException)
                {
                    if (token.IsCancellationRequested) break;
                    continue;
                }
                catch
                {
                    if (token.IsCancellationRequested) break;
                }

                if (client != null)
                    _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 3000;
                    client.SendTimeout = 3000;
                    NetworkStream stream = client.GetStream();
                    HttpRequestData request = await ReadRequestAsync(stream).ConfigureAwait(false);
                    if (request == null || string.IsNullOrWhiteSpace(request.RequestLine)) return;

                    string[] requestParts = request.RequestLine.Split(' ');
                    if (requestParts.Length < 2)
                    {
                        await RespondErrorAsync(stream, 400, "Bad Request", "Malformed HTTP request.", "text/plain", "Bad request").ConfigureAwait(false);
                        return;
                    }

                    string method = requestParts[0].ToUpperInvariant();
                    Uri uri;
                    try { uri = new Uri("http://127.0.0.1" + requestParts[1]); }
                    catch
                    {
                        await RespondErrorAsync(stream, 400, "Bad Request", "Invalid request URL.", "text/plain", "Bad request").ConfigureAwait(false);
                        return;
                    }

                    if (method == "OPTIONS")
                    {
                        await WriteResponseAsync(stream, 204, "No Content", "text/plain", "").ConfigureAwait(false);
                        return;
                    }

                    if (method != "GET" && method != "POST")
                    {
                        await RespondErrorAsync(stream, 405, "Method Not Allowed", "Unsupported HTTP method.", "application/json", "{\"error\":\"method_not_allowed\"}").ConfigureAwait(false);
                        return;
                    }

                    string suppliedToken = GetQueryValue(uri.Query, "token");
                    if (!FixedTimeEquals(_token, suppliedToken))
                    {
                        await RespondErrorAsync(stream, 403, "Forbidden", "Invalid API token.", "application/json", "{\"error\":\"forbidden\"}").ConfigureAwait(false);
                        return;
                    }

                    if (method == "GET" && uri.AbsolutePath == "/api/poll")
                    {
                        var items = new List<string>();
                        lock (_queueLock)
                        {
                            PendingCommand command;
                            while (items.Count < 32 && _commands.TryDequeue(out command))
                            {
                                if (DateTime.UtcNow - command.CreatedUtc <= TimeSpan.FromSeconds(3))
                                    items.Add("\"" + command.Action + "\"");
                            }
                        }

                        string body = "{\"commands\":[" + string.Join(",", items) + "]}";
                        await WriteResponseAsync(stream, 200, "OK", "application/json; charset=utf-8", body).ConfigureAwait(false);
                        return;
                    }

                    if (method == "POST" && uri.AbsolutePath == "/api/log")
                    {
                        ProcessScriptLogEvent(request.Body);
                        await WriteResponseAsync(stream, 202, "Accepted", "application/json; charset=utf-8", "{\"accepted\":true}").ConfigureAwait(false);
                        return;
                    }

                    await RespondErrorAsync(stream, 404, "Not Found", "Unknown API endpoint.", "application/json", "{\"error\":\"not_found\"}").ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    AppLogger.Error("HTTP request handling failed: " + exception.GetType().Name + ".");
                }
            }
        }

        private static async Task<HttpRequestData> ReadRequestAsync(NetworkStream stream)
        {
            var received = new List<byte>();
            var buffer = new byte[4096];
            int headerEnd = -1;

            while (headerEnd < 0)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (read <= 0) return null;
                for (int i = 0; i < read; i++) received.Add(buffer[i]);
                if (received.Count > MaximumHeaderBytes)
                    throw new InvalidDataException("HTTP header is too large.");
                headerEnd = FindHeaderEnd(received);
            }

            byte[] allBytes = received.ToArray();
            string headerText = Encoding.ASCII.GetString(allBytes, 0, headerEnd);
            string[] headerLines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (headerLines.Length == 0) return null;

            int contentLength = 0;
            for (int i = 1; i < headerLines.Length; i++)
            {
                int separator = headerLines[i].IndexOf(':');
                if (separator <= 0) continue;
                string name = headerLines[i].Substring(0, separator).Trim();
                string value = headerLines[i].Substring(separator + 1).Trim();
                if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(value, out contentLength);
            }

            if (contentLength < 0 || contentLength > MaximumBodyBytes)
                throw new InvalidDataException("HTTP body size is invalid.");

            int bodyStart = headerEnd + 4;
            int bodyAvailable = allBytes.Length - bodyStart;
            while (bodyAvailable < contentLength)
            {
                int read = await stream.ReadAsync(buffer, 0, Math.Min(buffer.Length, contentLength - bodyAvailable)).ConfigureAwait(false);
                if (read <= 0) throw new EndOfStreamException("HTTP request body ended unexpectedly.");
                var expanded = new byte[received.Count + read];
                received.CopyTo(expanded);
                Array.Copy(buffer, 0, expanded, received.Count, read);
                received = new List<byte>(expanded);
                allBytes = expanded;
                bodyAvailable += read;
            }

            string body = contentLength == 0 ? string.Empty : Encoding.UTF8.GetString(allBytes, bodyStart, contentLength);
            return new HttpRequestData { RequestLine = headerLines[0], Body = body };
        }

        private static int FindHeaderEnd(IList<byte> bytes)
        {
            for (int i = 3; i < bytes.Count; i++)
            {
                if (bytes[i - 3] == 13 && bytes[i - 2] == 10 && bytes[i - 1] == 13 && bytes[i] == 10)
                    return i - 3;
            }
            return -1;
        }

        private void ProcessScriptLogEvent(string json)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                var data = serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (data == null) return;

                string type = GetString(data, "type");
                string action = GetString(data, "action").ToUpperInvariant();
                string artist = GetString(data, "artist");
                string track = GetString(data, "track");
                string reason = GetString(data, "reason");
                string status = GetString(data, "status");

                if (type == "action_success" || type == "force_action_success")
                {
                    bool forced = type == "force_action_success" || GetBoolean(data, "forced");
                    string displayTrack = FormatTrack(artist, track);
                    AppLogger.Info(forced
                        ? "Successfully force-clicked " + action + " for: " + displayTrack + "."
                        : "Successfully clicked " + action + " for: " + displayTrack + ".");
                    if (_onActionSucceeded != null)
                    {
                        try { _onActionSucceeded(action.ToLowerInvariant(), forced); }
                        catch (Exception callbackException) { AppLogger.Error("Action success callback failed: " + callbackException.GetType().Name + "."); }
                    }
                }
                else if (type == "already_liked")
                {
                    AppLogger.Info("Already liked: " + FormatTrack(artist, track) + ". Skipped duplicate LIKE action.");
                }
                else if (type == "already_disliked")
                {
                    AppLogger.Info("Already disliked: " + FormatTrack(artist, track) + ". Skipped duplicate DISLIKE action.");
                }
                else if (type == "track_not_found")
                {
                    AppLogger.Error("Track metadata not found. Selectors tried: " + GetSelectors(data) + ".");
                }
                else if (type == "button_not_found")
                {
                    AppLogger.Error(action + " button not found. Selectors tried: " + GetSelectors(data) + ".");
                }
                else if (type == "button_click_failed")
                {
                    AppLogger.Error(action + " button click failed. Selectors checked: " + GetSelectors(data) + ".");
                }
                else if (type == "poll_error")
                {
                    AppLogger.Error("Polling request failed: " + Safe(reason, "Network error") + ".");
                }
                else if (type == "poll_http_error")
                {
                    AppLogger.Error("Polling request returned HTTP " + Safe(status, "unknown status") + ".");
                }
                else
                {
                    AppLogger.Error("Unknown userscript log event: " + Safe(type, "undefined") + ".");
                }
            }
            catch (Exception exception)
            {
                AppLogger.Error("Unable to parse userscript log event: " + exception.GetType().Name + ".");
            }
        }

        private static string FormatTrack(string artist, string track)
        {
            if (string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(track)) return "Unknown track";
            if (string.IsNullOrWhiteSpace(artist)) return Safe(track, "Unknown track");
            if (string.IsNullOrWhiteSpace(track)) return Safe(artist, "Unknown track");
            return Safe(artist, "Unknown artist") + " — " + Safe(track, "Unknown track");
        }

        private static bool GetBoolean(Dictionary<string, object> data, string key)
        {
            object value;
            if (!data.TryGetValue(key, out value) || value == null) return false;
            bool parsed;
            return bool.TryParse(Convert.ToString(value), out parsed) && parsed;
        }

        private static string GetSelectors(Dictionary<string, object> data)
        {
            object raw;
            if (!data.TryGetValue("selectors", out raw) || raw == null) return "not provided";
            var array = raw as object[];
            if (array == null) return Safe(Convert.ToString(raw), "not provided");
            var values = new List<string>();
            foreach (object item in array)
            {
                string value = Convert.ToString(item);
                if (!string.IsNullOrWhiteSpace(value)) values.Add("[" + Safe(value, "") + "]");
            }
            return values.Count == 0 ? "not provided" : string.Join(", ", values);
        }

        private static string GetString(Dictionary<string, object> data, string key)
        {
            object value;
            return data.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : string.Empty;
        }

        private static string Safe(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            return value.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private async Task RespondErrorAsync(NetworkStream stream, int statusCode, string reason,
            string diagnostic, string contentType, string body)
        {
            LogHttpError(statusCode, reason, diagnostic);
            await WriteResponseAsync(stream, statusCode, reason, contentType, body).ConfigureAwait(false);
        }

        private void LogHttpError(int statusCode, string reason, string diagnostic)
        {
            string key = statusCode + "|" + reason + "|" + diagnostic;
            DateTime now = DateTime.UtcNow;
            DateTime previous;
            if (_lastHttpErrorLog.TryGetValue(key, out previous) && now - previous < TimeSpan.FromSeconds(10)) return;
            _lastHttpErrorLog[key] = now;
            AppLogger.Error("HTTP " + statusCode + " " + reason + ": " + diagnostic);
        }

        private static string GetQueryValue(string query, string key)
        {
            if (string.IsNullOrEmpty(query)) return null;
            string[] pairs = query.TrimStart('?').Split('&');
            foreach (string pair in pairs)
            {
                int index = pair.IndexOf('=');
                string name = index >= 0 ? pair.Substring(0, index) : pair;
                if (!Uri.UnescapeDataString(name).Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
                return Uri.UnescapeDataString(index >= 0 ? pair.Substring(index + 1) : "");
            }
            return null;
        }

        private static bool FixedTimeEquals(string expected, string actual)
        {
            if (expected == null || actual == null || expected.Length != actual.Length) return false;
            int difference = 0;
            for (int i = 0; i < expected.Length; i++) difference |= expected[i] ^ actual[i];
            return difference == 0;
        }

        private static async Task WriteResponseAsync(NetworkStream stream, int statusCode,
            string reason, string contentType, string body)
        {
            byte[] payload = Encoding.UTF8.GetBytes(body ?? "");
            string headers =
                "HTTP/1.1 " + statusCode + " " + reason + "\r\n" +
                "Content-Type: " + contentType + "\r\n" +
                "Content-Length: " + payload.Length + "\r\n" +
                "Connection: close\r\n" +
                "Cache-Control: no-store, no-cache, must-revalidate\r\n" +
                "Access-Control-Allow-Origin: https://music.yandex.ru\r\n" +
                "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                "Access-Control-Allow-Headers: Content-Type\r\n" +
                "\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length).ConfigureAwait(false);
            if (payload.Length > 0)
                await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }
    }
}

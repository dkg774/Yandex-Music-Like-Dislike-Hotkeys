using System;
using System.IO;
using System.Media;

namespace YandexMusicLikeDislikeHotkeys
{
    /// <summary>Plays WAV notifications inside the desktop application process.</summary>
    internal sealed class SoundPlayback : IDisposable
    {
        private readonly object _sync = new object();
        private SoundPlayer _likePlayer;
        private SoundPlayer _dislikePlayer;
        private bool _disposed;

        public void Play(bool isLike, bool enabled, string path)
        {
            if (!enabled || string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            lock (_sync)
            {
                if (_disposed) return;
                try
                {
                    SoundPlayer previous = isLike ? _likePlayer : _dislikePlayer;
                    if (previous != null)
                    {
                        try { previous.Stop(); } catch { }
                        previous.Dispose();
                    }

                    var player = new SoundPlayer(path);
                    player.Load();
                    if (isLike) _likePlayer = player;
                    else _dislikePlayer = player;
                    player.Play();
                }
                catch (Exception exception)
                {
                    AppLogger.Error("Unable to play " + (isLike ? "like" : "dislike") + " sound: " + exception.GetType().Name + ".");
                }
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                if (_likePlayer != null) { try { _likePlayer.Stop(); } catch { } _likePlayer.Dispose(); _likePlayer = null; }
                if (_dislikePlayer != null) { try { _dislikePlayer.Stop(); } catch { } _dislikePlayer.Dispose(); _dislikePlayer = null; }
            }
        }
    }
}

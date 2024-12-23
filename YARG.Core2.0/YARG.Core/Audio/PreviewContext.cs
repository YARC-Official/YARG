using System;
using System.Threading.Tasks;
using System.Threading;
using YARG.Core.Logging;
using YARG.Core.Song;
using System.Diagnostics;

namespace YARG.Core.Audio
{
    public class PreviewContext : IDisposable
    {
        private const double DEFAULT_PREVIEW_DURATION = 30.0;
        private const double DEFAULT_START_TIME = 20.0;
        private const double DEFAULT_END_TIME = 50.0;

        public static async Task<PreviewContext?> Create(SongEntry entry, float volume, float speed, double delaySeconds, double fadeDuration, CancellationTokenSource token)
        {
            try
            {
                if (delaySeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }

                // Check if cancelled
                if (token.IsCancellationRequested)
                {
                    return null;
                }

                // Load the song
                var mixer = await Task.Run(() => entry.LoadPreviewAudio(speed));
                if (mixer == null || token.IsCancellationRequested)
                {
                    mixer?.Dispose();
                    return null;
                }

                double previewLength = mixer.Length;
                double previewStartTime = 0;
                if (mixer.Channels.Count > 0)
                {
                    double previewEndTime;
                    if ((entry.PreviewStartMilliseconds < 0 || entry.PreviewStartSeconds >= previewLength)
                    &&  (entry.PreviewEndMilliseconds <= 0  || entry.PreviewEndSeconds > previewLength))
                    {
                        if (DEFAULT_END_TIME <= previewLength)
                        {
                            previewStartTime = DEFAULT_START_TIME;
                            previewEndTime = DEFAULT_END_TIME;
                        }
                        else if (DEFAULT_PREVIEW_DURATION <= previewLength)
                        {
                            previewStartTime = (previewLength - DEFAULT_PREVIEW_DURATION) / 2;
                            previewEndTime = previewStartTime + DEFAULT_PREVIEW_DURATION;
                        }
                        else
                        {
                            previewStartTime = 0;
                            previewEndTime = previewLength;
                        }
                    }
                    else if (0 <= entry.PreviewStartSeconds && entry.PreviewStartSeconds < previewLength)
                    {
                        previewStartTime = entry.PreviewStartSeconds;
                        previewEndTime = entry.PreviewEndSeconds;
                        if (previewEndTime <= previewStartTime)
                        {
                            previewEndTime = previewStartTime + DEFAULT_PREVIEW_DURATION;
                        }

                        if (previewEndTime > previewLength)
                        {
                            previewEndTime = previewLength;
                        }
                    }
                    else
                    {
                        previewEndTime = entry.PreviewEndSeconds;
                        previewStartTime = previewEndTime - DEFAULT_PREVIEW_DURATION;
                        if (previewStartTime < 0)
                        {
                            previewStartTime = 0;
                        }
                    }
                    previewLength = previewEndTime - previewStartTime;
                }
                
                if (fadeDuration > previewLength / 4)
                {
                    fadeDuration = previewLength / 4;
                }
                return new PreviewContext(mixer, previewStartTime, previewLength, fadeDuration, volume, token);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error while loading song preview!");
                return null;
            }
        }

        private StemMixer _mixer;
        private Task _task;
        private readonly double _previewStartTime;
        private readonly double _previewLength;
        private readonly double _fadeDruation;
        private readonly float _volume;
        private readonly CancellationTokenSource _token;
        private bool _disposed;

        private PreviewContext(StemMixer mixer, double previewStartTime, double previewLength, double fadeDuration, float volume, CancellationTokenSource token)
        {
            _mixer = mixer;
            _previewStartTime = previewStartTime;
            _previewLength = previewLength;
            _fadeDruation = fadeDuration;
            _volume = volume;
            _token = token;

            _task = Task.Run(Loop);
        }

        public async void Stop()
        {
            _token.Cancel();
            await _task;
        }

        private async void Loop()
        {
            try
            {
                var watch = new Stopwatch();
                while (true)
                {
                    _mixer.SetPosition(_previewStartTime);
                    _mixer.FadeIn(_volume, _fadeDruation);
                    _mixer.Play(true);
                    watch.Restart();
                    while (watch.Elapsed.TotalSeconds < _previewLength - _fadeDruation && !_token.IsCancellationRequested)
                    {
                        if (_disposed)
                        {
                            return;
                        }
                        await Task.Delay(1);
                    }

                    watch.Restart();
                    _mixer.FadeOut(_fadeDruation);
                    while (watch.Elapsed.TotalSeconds < _fadeDruation)
                    {
                        if (_disposed)
                        {
                            return;
                        }
                        await Task.Delay(1);
                    }

                    _mixer.Pause();
                    if (_token.IsCancellationRequested)
                    {
                        Dispose();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error while looping song preview!");
            }
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing)
                {
                    _mixer.Dispose();
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}

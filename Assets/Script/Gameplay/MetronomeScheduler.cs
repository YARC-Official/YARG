using System;
using System.Collections.Generic;
using System.Diagnostics;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Gameplay
{
    /// <summary>
    /// Builds metronome events on the song timeline. Timed samples are mixed after tempo processing,
    /// keeping their original pitch and duration at every song speed.
    /// </summary>
    public sealed class MetronomeScheduler : IDisposable
    {
        private readonly struct Hit
        {
            public readonly double Time;
            public readonly MetronomePitch Pitch;

            public Hit(double time, MetronomePitch pitch)
            {
                Time = time;
                Pitch = pitch;
            }
        }

        private readonly StemMixer _mixer;
        private readonly List<Hit> _hits = new();
        private readonly double _songOffset;
        private OneShotChannel _hiChannel;
        private OneShotChannel _loChannel;

        public MetronomeScheduler(StemMixer mixer, SyncTrack sync, double songLength,
            double songOffset)
        {
            _mixer = mixer;
            _songOffset = songOffset;

            foreach (Beatline beatline in sync.Beatlines)
            {
                if (beatline.Type == BeatlineType.Measure && beatline.Time <= songLength)
                {
                    _hits.Add(new Hit(beatline.Time, MetronomePitch.Hi));
                }
            }

            for (uint tick = 0; ; tick += sync.Resolution)
            {
                double time = sync.TickToTime(tick);
                if (time > songLength)
                {
                    break;
                }

                _hits.Add(new Hit(time, MetronomePitch.Lo));
                if (uint.MaxValue - tick < sync.Resolution)
                {
                    break;
                }
            }

            _hits.Sort((left, right) => left.Time.CompareTo(right.Time));
            CreateChannels(SettingsManager.Settings.MetronomeSound.Value);
            SettingsManager.Settings.MetronomeSound.OnChange += OnSoundChanged;
            SettingsManager.Settings.MetronomeVolume.OnChange += OnVolumeChanged;
        }

        private void CreateChannels(MetronomeSample sample)
        {
            _hiChannel?.Dispose();
            _loChannel?.Dispose();

            _hiChannel = _mixer.CreateOneShotChannel(
                GlobalAudioHandler.CreateMetronomeStream(sample, MetronomePitch.Hi));
            _loChannel = _mixer.CreateOneShotChannel(
                GlobalAudioHandler.CreateMetronomeStream(sample, MetronomePitch.Lo));

            var stopwatch = Stopwatch.StartNew();
            foreach (Hit hit in _hits)
            {
                OneShotChannel channel = hit.Pitch == MetronomePitch.Hi
                    ? _hiChannel
                    : _loChannel;
                channel.Schedule(hit.Time + _songOffset);
            }
            SetVolume(sample);
            stopwatch.Stop();
            YargLogger.LogFormatDebug("Scheduled {0} DSP metronome hits in {1:0.00} ms",
                _hits.Count, stopwatch.Elapsed.TotalMilliseconds);
        }

        private void OnSoundChanged(MetronomeSample sample)
        {
            CreateChannels(sample);
        }

        private void OnVolumeChanged(float _)
        {
            SetVolume(SettingsManager.Settings.MetronomeSound.Value);
        }

        private void SetVolume(MetronomeSample sample)
        {
            double volume = GlobalAudioHandler.GetTrueVolume(SongStem.Metronome) *
                AudioHelpers.MetronomeSamples[(int) sample].Volume;
            _hiChannel.SetVolume(volume);
            _loChannel.SetVolume(volume);
        }

        public void Dispose()
        {
            SettingsManager.Settings.MetronomeSound.OnChange -= OnSoundChanged;
            SettingsManager.Settings.MetronomeVolume.OnChange -= OnVolumeChanged;
            _hiChannel.Dispose();
            _loChannel.Dispose();
        }
    }
}

using System;
using System.Collections.Generic;
using YARG.Audio.BASS;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Playback;
using YARG.Settings;

namespace YARG.Gameplay
{
    /// <summary>
    /// Builds metronome events on the song timeline. Timed samples are mixed after tempo processing,
    /// keeping their original pitch and duration at every song speed.
    /// </summary>
    public sealed class MetronomeScheduler : IDisposable
    {
        private readonly StemMixer _mixer;
        private double[] _hiChartTimes = Array.Empty<double>();
        private double[] _loChartTimes = Array.Empty<double>();
        private double[] _hiHits = Array.Empty<double>();
        private double[] _loHits = Array.Empty<double>();

        // Held as the concrete Bass type (rather than the OneShotChannel base from YARG.Core)
        // so Reschedule() can call UpdateSchedule() without a YARG.Core change. Bass is
        // currently the only audio backend, so this cast is safe.
        private BassOneShotChannel _hiChannel;
        private BassOneShotChannel _loChannel;
        private bool _scheduled;
        private bool _disposed;
        public bool IsDisposed => _disposed;

        /// <summary>
        /// Creates a metronome scheduler for the supplied mixer.
        /// </summary>
        public MetronomeScheduler(StemMixer mixer)
        {
            _mixer = mixer;
        }

        /// <summary>
        /// Schedules metronome hits for the song and begins responding to metronome settings.
        /// </summary>
        public void Schedule(SongRunner songRunner, SyncTrack sync, double songLength)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MetronomeScheduler));
            }
            if (_scheduled)
            {
                throw new InvalidOperationException("Metronome has already been scheduled.");
            }

            CacheChartTimes(sync, songLength, out _hiChartTimes, out _loChartTimes);
            RemapHits(songRunner);
            CreateChannels(SettingsManager.Settings.MetronomeSound.Value);
            SettingsManager.Settings.MetronomeSound.OnChange += OnSoundChanged;
            SettingsManager.Settings.MetronomeVolume.OnChange += OnVolumeChanged;
            SettingsManager.Settings.OutputChannelMetronome.OnChange += OnOutputChannelChanged;
            SettingsManager.Settings.OutputChannelDefault.OnChange += OnOutputChannelChanged;
            _scheduled = true;
        }

        /// <summary>
        /// Recomputes metronome hit times (e.g. after a live song offset change) and updates
        /// the already-scheduled playback channels in place. Cheap enough to call every frame,
        /// since it reuses the decoded metronome samples instead of rebuilding them, and only
        /// remaps the cached chart times through the current offset instead of re-walking the
        /// sync track.
        /// </summary>
        public void Reschedule(SongRunner songRunner, SyncTrack sync, double songLength)
        {
            if (_disposed || !_scheduled)
            {
                return;
            }

            RemapHits(songRunner);
            _hiChannel.UpdateSchedule(_hiHits);
            _loChannel.UpdateSchedule(_loHits);
        }

        // Which beatlines are hi/lo hits depends only on the chart and songLength, never on the
        // offset, so this only needs to run once (in Schedule()) instead of on every Reschedule().
        private static void CacheChartTimes(SyncTrack sync, double songLength,
            out double[] hiChartTimes, out double[] loChartTimes)
        {
            var hi = new List<double>();
            var lo = new List<double>();
            foreach (var beatline in sync.Beatlines)
            {
                if (beatline.Time > songLength)
                {
                    break;
                }

                var times = beatline.Type == BeatlineType.Measure ? hi : lo;
                times.Add(beatline.Time);
            }

            hiChartTimes = hi.ToArray();
            loChartTimes = lo.ToArray();
        }

        // Only the audio-time mapping depends on the offset, so Reschedule() only needs to redo
        // this cheap remap over the cached chart times, with no allocation once warmed up.
        private void RemapHits(SongRunner songRunner)
        {
            if (_hiHits.Length != _hiChartTimes.Length)
            {
                _hiHits = new double[_hiChartTimes.Length];
            }
            if (_loHits.Length != _loChartTimes.Length)
            {
                _loHits = new double[_loChartTimes.Length];
            }

            for (int i = 0; i < _hiChartTimes.Length; i++)
            {
                _hiHits[i] = songRunner.GetAudioPlaybackTime(_hiChartTimes[i]);
            }
            for (int i = 0; i < _loChartTimes.Length; i++)
            {
                _loHits[i] = songRunner.GetAudioPlaybackTime(_loChartTimes[i]);
            }
        }

        private void CreateChannels(MetronomeSample sample)
        {
            DisposeChannels();
            var hiStream = GlobalAudioHandler.CreateMetronomeStream(sample, MetronomePitch.Hi);
            var loStream = GlobalAudioHandler.CreateMetronomeStream(sample, MetronomePitch.Lo);

            int channelId = SettingsManager.Settings.OutputChannelMetronome.Value;
            if (channelId == -1)
            {
                channelId = SettingsManager.Settings.OutputChannelDefault.Value;
            }

            var outputChannel = GlobalAudioHandler.CreateOutputChannel(channelId);
            _hiChannel = (BassOneShotChannel) _mixer.CreateOneShotChannel(hiStream, _hiHits, outputChannel: outputChannel);
            _loChannel = (BassOneShotChannel) _mixer.CreateOneShotChannel(loStream, _loHits, outputChannel: outputChannel);
            SetVolume(sample);
        }

        private void OnSoundChanged(MetronomeSample sample)
        {
            CreateChannels(sample);
        }

        private void OnVolumeChanged(float _)
        {
            SetVolume(SettingsManager.Settings.MetronomeSound.Value);
        }

        private void OnOutputChannelChanged(int _)
        {
            CreateChannels(SettingsManager.Settings.MetronomeSound.Value);
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
            if (_disposed)
            {
                return;
            }

            SettingsManager.Settings.MetronomeSound.OnChange -= OnSoundChanged;
            SettingsManager.Settings.MetronomeVolume.OnChange -= OnVolumeChanged;
            SettingsManager.Settings.OutputChannelMetronome.OnChange -= OnOutputChannelChanged;
            SettingsManager.Settings.OutputChannelDefault.OnChange -= OnOutputChannelChanged;
            DisposeChannels();
            _disposed = true;
        }

        private void DisposeChannels()
        {
            _hiChannel?.Dispose();
            _loChannel?.Dispose();
        }
    }
}

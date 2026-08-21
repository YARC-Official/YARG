using System;
using System.Collections.Generic;
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
        private double[] _hiHits = Array.Empty<double>();
        private double[] _loHits = Array.Empty<double>();
        private OneShotChannel _hiChannel;
        private OneShotChannel _loChannel;
        private bool _scheduled;
        private bool _disposed;

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

            CreateSchedule(songRunner, sync, songLength, out _hiHits, out _loHits);
            CreateChannels(SettingsManager.Settings.MetronomeSound.Value);
            SettingsManager.Settings.MetronomeSound.OnChange += OnSoundChanged;
            SettingsManager.Settings.MetronomeVolume.OnChange += OnVolumeChanged;
            SettingsManager.Settings.OutputChannelMetronome.OnChange += OnOutputChannelChanged;
            SettingsManager.Settings.OutputChannelDefault.OnChange += OnOutputChannelChanged;
            _scheduled = true;
        }

        private static void CreateSchedule(SongRunner songRunner, SyncTrack sync, double songLength,
            out double[] hiHits, out double[] loHits)
        {
            var hi = new List<double>();
            var lo = new List<double>();
            foreach (var beatline in sync.Beatlines)
            {
                if (beatline.Time > songLength)
                {
                    break;
                }

                double audioTime = songRunner.GetAudioPlaybackTime(beatline.Time);
                var hits = beatline.Type == BeatlineType.Measure ? hi : lo;
                hits.Add(audioTime);
            }

            hiHits = hi.ToArray();
            loHits = lo.ToArray();
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
            _hiChannel = _mixer.CreateOneShotChannel(hiStream, _hiHits, outputChannel: outputChannel);
            _loChannel = _mixer.CreateOneShotChannel(loStream, _loHits, outputChannel: outputChannel);
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

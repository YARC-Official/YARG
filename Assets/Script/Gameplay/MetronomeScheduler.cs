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
        private readonly List<Hit> _hits;
        private OneShotChannel _hiChannel;
        private OneShotChannel _loChannel;

        public MetronomeScheduler(StemMixer mixer, SongRunner songRunner, SyncTrack sync,
            double songLength)
        {
            _mixer = mixer;
            _hits = CreateHits(songRunner, sync, songLength);

            CreateChannels(SettingsManager.Settings.MetronomeSound.Value);
            SettingsManager.Settings.MetronomeSound.OnChange += OnSoundChanged;
            SettingsManager.Settings.MetronomeVolume.OnChange += OnVolumeChanged;
        }

        private static List<Hit> CreateHits(SongRunner songRunner, SyncTrack sync, double songLength)
        {
            var hits = new List<Hit>();
            foreach (var beatline in sync.Beatlines)
            {
                if (beatline.Time > songLength)
                {
                    break;
                }

                var pitch = beatline.Type == BeatlineType.Measure ? MetronomePitch.Hi : MetronomePitch.Lo;
                double audioTime = songRunner.GetAudioPlaybackTime(beatline.Time);
                hits.Add(new Hit(audioTime, pitch));
            }
            return hits;
        }

        private void CreateChannels(MetronomeSample sample)
        {
            DisposeChannels();
            var hiStream = GlobalAudioHandler.CreateMetronomeStream(sample, MetronomePitch.Hi);
            var loStream = GlobalAudioHandler.CreateMetronomeStream(sample, MetronomePitch.Lo);
            _hiChannel = _mixer.CreateOneShotChannel(hiStream);
            _loChannel = _mixer.CreateOneShotChannel(loStream);
            foreach (var hit in _hits)
            {
                var channel = hit.Pitch == MetronomePitch.Hi ? _hiChannel : _loChannel;
                channel.AddScheduledPlay(hit.Time);
            }
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
            DisposeChannels();
        }

        private void DisposeChannels()
        {
            _hiChannel?.Dispose();
            _loChannel?.Dispose();
        }
    }
}

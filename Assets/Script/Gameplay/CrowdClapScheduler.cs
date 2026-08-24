using System;
using System.Collections.Generic;
using YARG.Audio.BASS;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Parsing;
using YARG.Playback;
using YARG.Settings;

namespace YARG.Gameplay
{
    /// <summary>
    /// Schedules crowd claps in the final playback mixer so their audible transient stays on beat.
    /// </summary>
    public sealed class CrowdClapScheduler : IDisposable
    {
        // Built-in crowd clap's dominant transient lands roughly 85 ms after sample start.
        // Start it early so that transient, rather than its quiet attack, lands on the beat.
        private const double OUTPUT_LEAD_TIME = 0.020;

        private readonly StemMixer _mixer;

        // Held as the concrete Bass type (rather than the OneShotChannel base from YARG.Core)
        // so Reschedule() can call UpdateSchedule() without a YARG.Core change. Bass is
        // currently the only audio backend, so this cast is safe.
        private BassOneShotChannel _channel;
        private bool _enabled;
        private bool _scheduled;
        private bool _disposed;

        public CrowdClapScheduler(StemMixer mixer)
        {
            _mixer = mixer;
        }

        public void Schedule(SongRunner songRunner, SyncTrack sync,
            IReadOnlyList<CrowdEvent> crowdEvents, double firstNoteTime, double lastNoteTime,
            double songLength)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CrowdClapScheduler));
            }
            if (_scheduled)
            {
                throw new InvalidOperationException("Crowd claps have already been scheduled.");
            }

            var plays = BuildPlayTimes(songRunner, sync, crowdEvents, firstNoteTime, lastNoteTime, songLength);

            int stream = GlobalAudioHandler.CreateSoundEffectStream(SfxSample.Clap);
            int channelId = SettingsManager.Settings.OutputChannelSfx.Value;
            if (channelId == -1)
            {
                channelId = SettingsManager.Settings.OutputChannelDefault.Value;
            }

            var outputChannel = GlobalAudioHandler.CreateOutputChannel(channelId);
            _channel = (BassOneShotChannel) _mixer.CreateOneShotChannel(stream, plays, OUTPUT_LEAD_TIME, outputChannel);
            _channel.SetEnabled(_enabled);
            SettingsManager.Settings.SfxVolume.OnChange += OnVolumeChanged;
            ApplyVolume();
            _scheduled = true;
        }

        /// <summary>
        /// Recomputes crowd clap hit times (e.g. after a live song offset change) and updates
        /// the already-scheduled playback channel in place. Cheap enough to call every frame,
        /// since it reuses the decoded clap sample instead of rebuilding it.
        /// </summary>
        public void Reschedule(SongRunner songRunner, SyncTrack sync,
            IReadOnlyList<CrowdEvent> crowdEvents, double firstNoteTime, double lastNoteTime,
            double songLength)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CrowdClapScheduler));
            }
            if (!_scheduled)
            {
                throw new InvalidOperationException("Crowd claps have not been scheduled yet.");
            }

            var plays = BuildPlayTimes(songRunner, sync, crowdEvents, firstNoteTime, lastNoteTime, songLength);
            _channel.UpdateSchedule(plays);
        }

        private static List<double> BuildPlayTimes(SongRunner songRunner, SyncTrack sync,
            IReadOnlyList<CrowdEvent> crowdEvents, double firstNoteTime, double lastNoteTime,
            double songLength)
        {
            var events = new List<CrowdEvent>(crowdEvents);
            events.Sort((a, b) => a.Time.CompareTo(b.Time));

            var plays = new List<double>();
            var clapState = ClapState.Clap;
            int eventIndex = 0;
            foreach (var beatline in sync.Beatlines)
            {
                if (beatline.Time > songLength)
                {
                    break;
                }

                while (eventIndex < events.Count && events[eventIndex].Time <= beatline.Time)
                {
                    if (events[eventIndex].Type == CrowdEvent.CrowdEventType.Clap)
                    {
                        clapState = events[eventIndex].ClapState;
                    }
                    eventIndex++;
                }

                if (beatline.Type is not (BeatlineType.Measure or BeatlineType.Strong) ||
                    beatline.Time < firstNoteTime || beatline.Time > lastNoteTime ||
                    clapState == ClapState.NoClap)
                {
                    continue;
                }

                plays.Add(songRunner.GetAudioPlaybackTime(beatline.Time));
            }

            return plays;
        }

        public void SetEnabled(bool enabled)
        {
            if (_disposed || _enabled == enabled)
            {
                return;
            }

            _enabled = enabled;
            _channel?.SetEnabled(enabled);
        }

        private void OnVolumeChanged(float _)
        {
            ApplyVolume();
        }

        private void ApplyVolume()
        {
            double volume = GlobalAudioHandler.GetTrueVolume(SongStem.Sfx) *
                AudioHelpers.SfxSamples[(int) SfxSample.Clap].Volume;
            _channel?.SetVolume(volume);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            SettingsManager.Settings.SfxVolume.OnChange -= OnVolumeChanged;
            _channel?.Dispose();
            _disposed = true;
        }
    }
}

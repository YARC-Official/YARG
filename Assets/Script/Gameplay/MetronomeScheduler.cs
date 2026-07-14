using System;
using System.Collections.Generic;
using System.Diagnostics;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Logging;
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
        private const int HI_SAMPLE = 0;
        private const int LO_SAMPLE = 1;

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
        private readonly TimedSampleSchedule _schedule;
        private readonly SongRunner _songRunner;
        private readonly List<Hit> _hits = new();
        private readonly double _songOffset;

        public MetronomeScheduler(StemMixer mixer, SongRunner songRunner, SyncTrack sync,
            double songLength, double songOffset)
        {
            _mixer = mixer;
            _schedule = mixer.CreateTimedSampleSchedule();
            _songRunner = songRunner;
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
            SetSamples(SettingsManager.Settings.MetronomeSound.Value);
            _songRunner.AudioPrepared += Prepare;
            SettingsManager.Settings.MetronomeSound.OnChange += OnSoundChanged;
            SettingsManager.Settings.MetronomeVolume.OnChange += OnVolumeChanged;
        }

        private void Prepare(double fromAudioTime)
        {
            var stopwatch = Stopwatch.StartNew();
            _schedule.Clear();
            SetVolume(SettingsManager.Settings.MetronomeSound.Value);
            int scheduledHits = 0;
            foreach (Hit hit in _hits)
            {
                double hitAudioTime = hit.Time + _songOffset;
                if (hitAudioTime < fromAudioTime)
                {
                    continue;
                }

                int sampleId = hit.Pitch == MetronomePitch.Hi ? HI_SAMPLE : LO_SAMPLE;
                if (_schedule.Schedule(sampleId, hitAudioTime))
                {
                    scheduledHits++;
                }
            }
            _schedule.Commit();
            stopwatch.Stop();
            YargLogger.LogFormatDebug("Scheduled {0} DSP metronome hits in {1:0.00} ms",
                scheduledHits, stopwatch.Elapsed.TotalMilliseconds);
        }

        private void OnSoundChanged(MetronomeSample sample)
        {
            SetSamples(sample);
            Prepare(_mixer.GetPosition());
        }

        private void OnVolumeChanged(float _)
        {
            SetVolume(SettingsManager.Settings.MetronomeSound.Value);
        }

        private void SetVolume(MetronomeSample sample)
        {
            double volume = GlobalAudioHandler.GetTrueVolume(SongStem.Metronome) *
                AudioHelpers.MetronomeSamples[(int) sample].Volume;
            _schedule.SetVolume(volume);
        }

        private void SetSamples(MetronomeSample sample)
        {
            _schedule.SetSample(HI_SAMPLE,
                GlobalAudioHandler.CreateMetronomeStream(sample, MetronomePitch.Hi));
            _schedule.SetSample(LO_SAMPLE,
                GlobalAudioHandler.CreateMetronomeStream(sample, MetronomePitch.Lo));
        }

        public void Dispose()
        {
            _songRunner.AudioPrepared -= Prepare;
            SettingsManager.Settings.MetronomeSound.OnChange -= OnSoundChanged;
            SettingsManager.Settings.MetronomeVolume.OnChange -= OnVolumeChanged;
            _schedule.Dispose();
        }
    }
}

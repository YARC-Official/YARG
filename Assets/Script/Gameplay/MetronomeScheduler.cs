using System;
using System.Collections.Generic;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Playback;
using YARG.Settings;

namespace YARG.Gameplay
{
    /// <summary>
    /// Owns gameplay metronome scheduling. Hits enter the song mixer and therefore share its
    /// buffering, tempo processing, and synchronization corrections.
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
        private readonly SongRunner _songRunner;
        private readonly List<Hit> _hits = new();
        private readonly double _songOffset;

        public MetronomeScheduler(StemMixer mixer, SongRunner songRunner, SyncTrack sync,
            double songLength, double songOffset)
        {
            _mixer = mixer;
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
            _songRunner.AudioPrepared += Schedule;
            SettingsManager.Settings.MetronomeSound.OnChange += OnSoundChanged;
            SettingsManager.Settings.MetronomeVolume.OnChange += OnVolumeChanged;
        }

        private void Schedule(double fromAudioTime)
        {
            MetronomeSample sample = SettingsManager.Settings.MetronomeSound.Value;
            foreach (Hit hit in _hits)
            {
                double audioTime = hit.Time + _songOffset;
                if (audioTime < fromAudioTime)
                {
                    continue;
                }

                int stream = GlobalAudioHandler.CreateMetronomeStream(sample, hit.Pitch);
                _mixer.ScheduleOneShot(stream, audioTime);
            }
        }

        private void OnSoundChanged(MetronomeSample _)
        {
            Reschedule();
        }

        private void OnVolumeChanged(float _)
        {
            Reschedule();
        }

        private void Reschedule()
        {
            _mixer.ClearScheduledOneShots();
            Schedule(_mixer.GetPosition());
        }

        public void Dispose()
        {
            _songRunner.AudioPrepared -= Schedule;
            SettingsManager.Settings.MetronomeSound.OnChange -= OnSoundChanged;
            SettingsManager.Settings.MetronomeVolume.OnChange -= OnVolumeChanged;
        }
    }
}

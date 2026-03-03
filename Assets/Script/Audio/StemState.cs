using YARG.Core.Audio;
using YARG.Settings;

namespace YARG.Audio
{
    public class StemState
    {
        private const double DEFAULT_VOLUME = 1.0;

        public readonly SongStem Stem;
        private         int      _total;
        private         int      _audible;
        private         int      _reverbCount;

        public double Volume => GetVolumeSetting();

        public StemState(SongStem stem)
        {
            Stem = stem;
        }

        public void RegisterTrack()
        {
            _total++;
            _audible++;
        }

        public void RegisterBackground()
        {
            _total += 2;
            _audible += 2;
        }

        public double SetMute(bool muted)
        {
            if (muted)
            {
                --_audible;
            }
            else if (_audible < _total)
            {
                ++_audible;
            }

            return Volume * _audible / _total;
        }

        public bool SetReverb(bool reverb)
        {
            if (reverb)
            {
                _reverbCount++;
            }
            else if (_reverbCount > 0)
            {
                _reverbCount--;
            }

            return _reverbCount > 0;
        }

        private double GetVolumeSetting()
        {
            return Stem switch
            {
                SongStem.Guitar    => SettingsManager.Settings.GuitarVolume.Value,
                SongStem.Rhythm    => SettingsManager.Settings.RhythmVolume.Value,
                SongStem.Bass      => SettingsManager.Settings.BassVolume.Value,
                SongStem.Keys      => SettingsManager.Settings.KeysVolume.Value,
                SongStem.Drums     => SettingsManager.Settings.DrumsVolume.Value,
                SongStem.Vocals    => SettingsManager.Settings.VocalsVolume.Value,
                SongStem.Song      => SettingsManager.Settings.SongVolume.Value,
                SongStem.Crowd     => SettingsManager.Settings.CrowdVolume.Value,
                SongStem.Sfx       => SettingsManager.Settings.SfxVolume.Value,
                SongStem.DrumSfx   => SettingsManager.Settings.DrumSfxVolume.Value,
                SongStem.Metronome => SettingsManager.Settings.MetronomeVolume.Value,
                _                  => DEFAULT_VOLUME
            };
        }
    }
}

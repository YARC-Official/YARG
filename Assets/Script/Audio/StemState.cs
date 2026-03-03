using YARG.Core.Audio;
using YARG.Settings;

namespace YARG.Audio
{
    public class StemState
    {
        private const double DEFAULT_VOLUME = 1.0;

        public readonly SongStem Stem;
        public int Total;
        public int Audible;
        public int ReverbCount;

        public double Volume => GetVolumeSetting();

        public StemState(SongStem stem)
        {
            Stem = stem;
        }

        public double SetMute(bool muted)
        {
            if (muted)
            {
                --Audible;
            }
            else if (Audible < Total)
            {
                ++Audible;
            }

            return Volume * Audible / Total;
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

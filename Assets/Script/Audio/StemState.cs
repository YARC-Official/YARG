using YARG.Core.Audio;
using YARG.Settings;

namespace YARG.Audio
{
    public class StemState
    {
        public readonly SongStem Stem;
        private         int      _total;
        private         int      _audible;
        private         int      _reverbCount;

        public double Volume => SettingsManager.Settings.GetVolumeSetting(Stem);

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
    }
}

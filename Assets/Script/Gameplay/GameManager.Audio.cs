using System.Collections.Generic;
using System.Linq;
using YARG.Audio;
using YARG.Core.Audio;
using YARG.Playback;
using YARG.Settings;

namespace YARG.Gameplay
{
    public partial class GameManager
    {
        private const double DEFAULT_VOLUME = 1.0;

        private SongStem _backgroundStem;
        private bool     _hasCrowdStem;
        private StemVolumeLinker _volumeLinker;

        private void LoadAudio()
        {
            _mixer = Song.LoadAudio(GlobalVariables.State.SongSpeed, DEFAULT_VOLUME);
            if (_mixer == null)
            {
                _loadState = LoadFailureState.Error;
                _loadFailureMessage = "Failed to load audio!";
                return;
            }

            _volumeLinker = new StemVolumeLinker(_mixer, SettingsManager.Settings);

            var mixerStems = new HashSet<SongStem>();
            foreach (var stem in _mixer.Stems)
            {
                mixerStems.Add(stem);
            }

            _hasCrowdStem = mixerStems.Contains(SongStem.Crowd);
            _backgroundStem = mixerStems.Count > 1 ? SongStem.Song : mixerStems.First();
            _mixerStems = mixerStems;
        }

        public void ChangeStarPowerStatus(bool active)
        {
            if (SettingsManager.Settings.UseCrowdFx.Value == CrowdFxMode.Disabled)
            {
                return;
            }

            StarPowerActivations += active ? 1 : -1;
            if (StarPowerActivations < 0)
            {
                StarPowerActivations = 0;
            }
        }

        private void RestoreCrowdAudio()
        {
            if (_hasCrowdStem)
            {
                var volume = SettingsManager.Settings.GetVolumeSettingValue(SongStem.Crowd);
                _mixer?.SetVolume(SongStem.Crowd, volume);
            }
        }

        public void ChangeCrowdMuteState(bool muted, float duration = 0.0f)
        {
            if (!_hasCrowdStem)
            {
                return;
            }

            double volume = muted ? 0.0 : SettingsManager.Settings.GetVolumeSettingValue(SongStem.Crowd);
            _mixer?.SetVolume(SongStem.Crowd, volume, duration);
        }

    }
}

using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using YARG.Core.Audio;
using YARG.Playback;
using YARG.Settings;

namespace YARG.Gameplay
{
    public partial class GameManager
    {
        private const double DEFAULT_VOLUME = 1.0;

        private SongStem                               _backgroundStem;
        private TweenerCore<double, double, NoOptions> _crowdVolumeTween;
        private bool                                   _hasCrowdStem;

        private void LoadAudio()
        {
            _mixer = Song.LoadAudio(GlobalVariables.State.SongSpeed, DEFAULT_VOLUME);
            if (_mixer == null)
            {
                _loadState = LoadFailureState.Error;
                _loadFailureMessage = "Failed to load audio!";
                return;
            }

            var mixerStems = new HashSet<SongStem>();
            foreach (var channel in _mixer.Channels)
            {
                mixerStems.Add(channel.Stem);
            }

            _hasCrowdStem = mixerStems.Contains(SongStem.Crowd);
            _backgroundStem = mixerStems.Count > 1 ? SongStem.Song : mixerStems.First();
            _mixerStems = mixerStems;
        }

        public void ChangeStarPowerStatus(bool active)
        {
            if (SettingsManager.Settings.UseCrowdFx.Value == CrowdFxMode.Disabled)
                return;

            StarPowerActivations += active ? 1 : -1;
            if (StarPowerActivations < 0)
                StarPowerActivations = 0;
        }

        private void RestoreCrowdAudio()
        {
            _crowdVolumeTween?.Kill();
            if (_hasCrowdStem)
            {
                MixerAudioHandler.SetVolumeSetting(SongStem.Crowd, SettingsManager.Settings.CrowdVolume.Value);
            }
        }

        public void ChangeCrowdMuteState(bool muted, float duration = 0.0f)
        {
            if (!_hasCrowdStem)
            {
                return;
            }

            double volume = muted ? 0.0 : SettingsManager.Settings.CrowdVolume.Value;

            if (duration <= 0.0f)
            {
                MixerAudioHandler.SetVolumeSetting(SongStem.Crowd, volume);
                return;
            }

            if (_crowdVolumeTween == null || !_crowdVolumeTween.IsPlaying())
            {
                _crowdVolumeTween = DOTween.To(
                    () => MixerAudioHandler.GetVolumeSetting(SongStem.Crowd),
                    x => MixerAudioHandler.SetVolumeSetting(SongStem.Crowd, x),
                    volume, duration);
            }
            else
            {
                _crowdVolumeTween.ChangeEndValue(volume);
            }
        }

    }
}

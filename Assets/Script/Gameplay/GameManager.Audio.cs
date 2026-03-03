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
        public class StemState
        {
            private SongStem _stem;
            public double Volume => GetVolumeSetting();
            public int Total;
            public int Audible;
            public StemState(SongStem stem)
            {
                _stem = stem;
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
                return _stem switch
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

        private readonly Dictionary<SongStem, StemState>        _stemStates = new();
        private          SongStem                               _backgroundStem;
        private          TweenerCore<double, double, NoOptions> _volumeTween;
        private          TweenerCore<double, double, NoOptions> _crowdVolumeTween;

        private void LoadAudio()
        {
            _stemStates.Clear();
            _mixer = Song.LoadAudio(GlobalVariables.State.SongSpeed, DEFAULT_VOLUME);
            if (_mixer == null)
            {
                _loadState = LoadFailureState.Error;
                _loadFailureMessage = "Failed to load audio!";
                return;
            }

            _backgroundStem = SongStem.Song;
            foreach (var channel in _mixer.Channels)
            {
                var stemState = new StemState(channel.Stem);
                _stemStates.Add(channel.Stem, stemState);
            }

            _backgroundStem = _stemStates.Count > 1 ? SongStem.Song : _stemStates.First().Key;
        }

        public void ChangeStarPowerStatus(bool active)
        {
            if (SettingsManager.Settings.UseCrowdFx.Value == CrowdFxMode.Disabled)
                return;

            StarPowerActivations += active ? 1 : -1;
            if (StarPowerActivations < 0)
                StarPowerActivations = 0;
        }

        public void ChangeStemMuteState(SongStem stem, bool muted, float duration = 0.0f)
        {
            var setting = SettingsManager.Settings.MuteOnMiss.Value;
            if (setting == AudioFxMode.Off
            || !_stemStates.TryGetValue(stem, out var state)
            || (setting == AudioFxMode.MultitrackOnly && stem == _backgroundStem))
            {
                return;
            }

            double volume = state.SetMute(muted);

            if (duration <= 0.0f)
            {
                MixerAudioHandler.SetVolumeSetting(stem, volume);
                return;
            }

            if (_volumeTween == null || !_volumeTween.IsPlaying())
            {
                _volumeTween = DOTween.To(() => MixerAudioHandler.GetVolumeSetting(stem),
                    x => MixerAudioHandler.SetVolumeSetting(stem, x), volume, duration);
            }
            else
            {
                _volumeTween.ChangeEndValue(volume);
            }
        }

        public void ChangeCrowdMuteState(bool muted, float duration = 0.0f)
        {
            if (!_stemStates.ContainsKey(SongStem.Crowd))
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

using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;
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
            public int ReverbCount;
            public float WhammyPitch;

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

            public bool SetReverb(bool reverb)
            {
                if (reverb)
                {
                    ++ReverbCount;
                }
                else if (ReverbCount > 0)
                {
                    --ReverbCount;
                }
                return ReverbCount > 0;
            }

            public float SetWhammyPitch(float percent)
            {
                // TODO: Would be nice to handle multiple inputs
                // but for now last one wins
                WhammyPitch = Mathf.Clamp01(percent);
                return WhammyPitch;
            }

            private double GetVolumeSetting()
            {
                return _stem switch
                {
                    SongStem.Guitar => SettingsManager.Settings.GuitarVolume.Value,
                    SongStem.Rhythm => SettingsManager.Settings.RhythmVolume.Value,
                    SongStem.Bass   => SettingsManager.Settings.BassVolume.Value,
                    SongStem.Keys   => SettingsManager.Settings.KeysVolume.Value,
                    SongStem.Drums
                        or SongStem.Drums1
                        or SongStem.Drums2
                        or SongStem.Drums3
                        or SongStem.Drums4
                        => SettingsManager.Settings.DrumsVolume.Value,
                    SongStem.Vocals
                        or SongStem.Vocals1
                        or SongStem.Vocals2
                        => SettingsManager.Settings.VocalsVolume.Value,
                    SongStem.Song    => SettingsManager.Settings.SongVolume.Value,
                    SongStem.Crowd   => SettingsManager.Settings.CrowdVolume.Value,
                    SongStem.Sfx     => SettingsManager.Settings.SfxVolume.Value,
                    SongStem.DrumSfx => SettingsManager.Settings.DrumSfxVolume.Value,
                    _                => DEFAULT_VOLUME
                };
            }
        }

        private readonly Dictionary<SongStem, StemState>        _stemStates = new();
        private          SongStem                               _backgroundStem;
        private          TweenerCore<double, double, NoOptions> _volumeTween;

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
                switch (channel.Stem)
                {
                    case SongStem.Drums:
                    case SongStem.Drums1:
                    case SongStem.Drums2:
                    case SongStem.Drums3:
                    case SongStem.Drums4:
                        _stemStates.TryAdd(SongStem.Drums, stemState);
                        break;
                    case SongStem.Vocals:
                    case SongStem.Vocals1:
                    case SongStem.Vocals2:
                        _stemStates.TryAdd(SongStem.Vocals, stemState);
                        break;
                    default:
                        _stemStates.Add(channel.Stem, stemState);
                        break;
                }
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
                GlobalAudioHandler.SetVolumeSetting(stem, volume);
                return;
            }

            if (_volumeTween == null || !_volumeTween.IsPlaying())
            {
                _volumeTween = DOTween.To(() => GlobalAudioHandler.GetVolumeSetting(stem),
                    x => GlobalAudioHandler.SetVolumeSetting(stem, x), volume, duration);
            }
            else
            {
                _volumeTween.ChangeEndValue(volume);
            }
        }

        public void ChangeStemReverbState(SongStem stem, bool reverb)
        {
            var setting = SettingsManager.Settings.UseStarpowerFx.Value;
            if (setting == AudioFxMode.Off)
            {
                return;
            }

            StemState state;
            while (!_stemStates.TryGetValue(stem, out state))
            {
                if (stem == _backgroundStem)
                {
                    return;
                }
                stem = _backgroundStem;
            }

            if (setting == AudioFxMode.MultitrackOnly && stem == _backgroundStem)
            {
                return;
            }

            bool reverbActive = state.SetReverb(reverb);
            GlobalAudioHandler.SetReverbSetting(stem, reverbActive);
        }

        public void ChangeStemWhammyPitch(SongStem stem, float percent)
        {
            // If Whammy FX is turned off, ignore.
            if (!SettingsManager.Settings.UseWhammyFx.Value)
            {
                return;
            }

            // If the specified stem is the same as the background stem,
            // ignore the request. This may be a chart without separate
            // stems for each instrument. In that scenario we don't want
            // to pitch bend because we'd be bending the entire track.
            if (stem == _backgroundStem)
            {
                return;
            }

            // If we can't get the state for the stem, bail.
            if (!_stemStates.TryGetValue(stem, out var state))
            {
                return;
            }

            // Set the pitch
            float percentActive = state.SetWhammyPitch(percent);
            GlobalAudioHandler.SetWhammyPitchSetting(stem, percentActive);
        }
    }
}
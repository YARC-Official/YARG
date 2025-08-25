using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Settings;

namespace YARG.Gameplay
{
    public partial class GameManager
    {
        private const double DEFAULT_VOLUME = 1.0;
        public class StemState
        {
            public readonly double Volume;
            public int Total;
            public int Audible;
            public int ReverbCount;
            public float WhammyPitch;

            public StemState(double volume)
            {
                Volume = volume;
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

            public double CalculateVolumeSetting()
            {
                return Volume * Audible / Total;
            }
        }

        private readonly Dictionary<SongStem, StemState> _stemStates = new();
        private SongStem _backgroundStem;
        private int _starPowerActivations = 0;

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
                double volume = GlobalAudioHandler.GetVolumeSetting(channel.Stem);
                var stemState = new StemState(volume);
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

        private void StarPowerClap()
        {
            if (_starPowerActivations < 1)
            {
                return;
            }

            GlobalAudioHandler.PlaySoundEffect(SfxSample.Clap);
        }

        public void ChangeStarPowerStatus(bool active)
        {
            if (!SettingsManager.Settings.ClapsInStarpower.Value)
                return;

            _starPowerActivations += active ? 1 : -1;
            if (_starPowerActivations < 0)
                _starPowerActivations = 0;
        }

        public void ChangeStemMuteState(SongStem stem, bool muted)
        {
            var setting = SettingsManager.Settings.MuteOnMiss.Value;
            if (setting == AudioFxMode.Off
            || !_stemStates.TryGetValue(stem, out var state)
            || (setting == AudioFxMode.MultitrackOnly && stem == _backgroundStem))
            {
                return;
            }

            double volume = state.SetMute(muted);
            GlobalAudioHandler.SetVolumeSetting(stem, volume);
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
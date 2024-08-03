﻿using System.Collections.Generic;
using System.Linq;
using YARG.Core.Audio;
using YARG.Core.Chart;
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

        private void StarPowerClap(Beatline beat)
        {
            if (_starPowerActivations < 1 || beat.Type == BeatlineType.Weak)
                return;

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
    }
}
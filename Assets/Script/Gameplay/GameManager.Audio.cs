using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using YARG.Audio;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Helpers.Extensions;
using YARG.Settings;

namespace YARG.Gameplay
{
    public partial class GameManager
    {
        public class StemState
        {
            public int Total;
            public int Audible;
            public int ReverbCount;
        }

        private readonly Dictionary<SongStem, StemState> _stemStates = new();

        private int _starPowerActivations = 0;

        private void LoadAudio()
        {
            _stemStates.Clear();
            AudioManager.UseMinimumStemVolume = Song.Source.Str.ToLowerInvariant() == "yarg";
            _mixer = Song.LoadAudio(AudioManager.Instance, GlobalVariables.State.SongSpeed);
            if (_mixer == null)
            {
                _loadState = LoadFailureState.Error;
                _loadFailureMessage = "Failed to load audio!";
                return;
            }

            _mixer.SongEnd += OnAudioEnd;
            foreach (var channel in _mixer.Channels)
            {
                _stemStates.Add(channel.Stem, new StemState());
            }

            if (_stemStates.TryGetValue(SongStem.Song, out var state))
            {
                // Ensures it will still play *somewhat*, even if all players mute
                state.Total = 1;
                state.Audible = 1;
            }
        }

        private void StarPowerClap(Beatline beat)
        {
            if (_starPowerActivations < 1 || beat.Type == BeatlineType.Weak)
                return;

            AudioManager.PlaySoundEffect(SfxSample.Clap);
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
            if (!SettingsManager.Settings.MuteOnMiss.Value || !_stemStates.TryGetValue(stem, out var state))
            {
                return;
            }

            if (muted)
            {
                --state.Audible;
            }
            else if (state.Audible < state.Total)
            {
                ++state.Audible;
            }

            double volume = (double) state.Audible / state.Total;
            AudioManager.SetVolumeSetting(stem, volume);
        }

        public void ChangeStemReverbState(SongStem stem, bool reverb)
        {
            var setting = SettingsManager.Settings.UseStarpowerFx.Value;
            if (setting == StarPowerFxMode.Off)
            {
                return;
            }

            StemState state;
            while (!_stemStates.TryGetValue(stem, out state))
            {
                if (stem == SongStem.Song)
                {
                    return;
                }
                stem = SongStem.Song;
            }

            if (setting == StarPowerFxMode.MultitrackOnly && stem == SongStem.Song)
            {
                return;
            }

            if (reverb)
            {
                ++state.ReverbCount;
            }
            else if (state.ReverbCount > 0)
            {
                --state.ReverbCount;
            }

            bool reverbActive = state.ReverbCount > 0;
            AudioManager.SetReverbSetting(stem, reverbActive);
        }

        private void OnAudioEnd()
        {
            EndSong().Forget();
        }
    }
}
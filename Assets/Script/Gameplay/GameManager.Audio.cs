﻿using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Helpers.Extensions;
using YARG.Settings;

namespace YARG.Gameplay
{
    public partial class GameManager
    {
        public struct StemState
        {
            public int Total;
            public int Muted;
            public int ReverbCount;

            public float GetVolumeLevel()
            {
                if (Total == 0)
                {
                    return 1f;
                }

                return (float) (Total - Muted) / Total;
            }
        }

        private readonly Dictionary<SongStem, StemState> _stemStates = new();

        private int _starPowerActivations = 0;

        private async UniTask LoadAudio()
        {
            // The stem states are initialized in "CreatePlayers"
            _stemStates.Clear();
            _stemStates.Add(SongStem.Song, new StemState
            {
                Total = 1
            });

            await UniTask.RunOnThreadPool(() =>
            {
                try
                {
                    Song.LoadAudio(GlobalVariables.AudioManager, GlobalVariables.Instance.SongSpeed);
                    GlobalVariables.AudioManager.SongEnd += OnAudioEnd;
                }
                catch (Exception ex)
                {
                    _loadState = LoadFailureState.Error;
                    _loadFailureMessage = "Failed to load audio!";
                    Debug.LogException(ex, this);
                }
            });

            bool isYargSong = Song.Source.Str.ToLowerInvariant() == "yarg";
            GlobalVariables.AudioManager.Options.UseMinimumStemVolume = isYargSong;

            if (_loadState != LoadFailureState.None) return;

            double audioLength = GlobalVariables.AudioManager.AudioLengthD;
            double chartLength = Chart.GetEndTime();
            double endTime = Chart.GetEndEvent()?.Time ?? -1;

            // - Chart < Audio < [end] -> Audio
            // - Audio < Chart < [end] -> Chart
            // - [end] < Chart < Audio -> Audio
            // - [end] < Audio < Chart -> Chart
            if ((endTime >= audioLength && endTime >= chartLength) ||
                endTime <= audioLength && endTime <= chartLength)
            {
                SongLength = Math.Max(audioLength, chartLength);
            }
            // - Audio < [end] < Chart -> Chart
            // - Chart < [end] < Audio -> [end]
            else
            {
                SongLength = Math.Max(chartLength, endTime);
            }

            SongLength += SONG_END_DELAY;
            _songLoaded?.Invoke();
        }

        private void StarPowerClap(Beatline beat)
        {
            if (_starPowerActivations < 1 || beat.Type == BeatlineType.Weak)
                return;

            GlobalVariables.AudioManager.PlaySoundEffect(SfxSample.Clap);
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
            if (!SettingsManager.Settings.MuteOnMiss.Value) return;

            if (!_stemStates.TryGetValue(stem, out var state)) return;

            if (muted)
            {
                state.Muted++;
            }
            else
            {
                state.Muted = Math.Max(0, state.Muted - 1);
            }
            var volume = state.GetVolumeLevel();
            GlobalVariables.AudioManager.SetStemVolume(stem, volume);

            // Mute all of the stems for songs with multiple drum stems
            // TODO: Implement proper drum stem muting
            if (stem == SongStem.Drums)
            {
                GlobalVariables.AudioManager.SetStemVolume(SongStem.Drums1, volume);
                GlobalVariables.AudioManager.SetStemVolume(SongStem.Drums2, volume);
                GlobalVariables.AudioManager.SetStemVolume(SongStem.Drums3, volume);
                GlobalVariables.AudioManager.SetStemVolume(SongStem.Drums4, volume);
            }
        }

        public void ChangeStemReverbState(SongStem stem, bool reverb)
        {
            if (!SettingsManager.Settings.UseStarpowerFx.Value) return;

            if (!_stemStates.TryGetValue(stem, out var state)) return;

            if (reverb)
            {
                state.ReverbCount++;
            }
            else
            {
                state.ReverbCount = Math.Max(0, state.ReverbCount - 1);
            }

            bool reverbActive = state.ReverbCount > 0;

            GlobalVariables.AudioManager.ApplyReverb(stem, reverbActive);

            // Reverb all of the stems for songs with multiple drum stems
            // TODO: Implement proper drum stem reverbing
            if (stem == SongStem.Drums)
            {
                GlobalVariables.AudioManager.ApplyReverb(SongStem.Drums1, reverbActive);
                GlobalVariables.AudioManager.ApplyReverb(SongStem.Drums2, reverbActive);
                GlobalVariables.AudioManager.ApplyReverb(SongStem.Drums3, reverbActive);
                GlobalVariables.AudioManager.ApplyReverb(SongStem.Drums4, reverbActive);
            }
        }

        private void OnAudioEnd()
        {
            EndSong().Forget();
        }
    }
}
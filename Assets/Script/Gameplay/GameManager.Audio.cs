using System;
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
                { Total = 1});

            bool isYargSong = Song.Source.Str.ToLowerInvariant() == "yarg";
            GlobalVariables.AudioManager.Options.UseMinimumStemVolume = isYargSong;

            await UniTask.RunOnThreadPool(() =>
            {
                try
                {
                    Song.LoadAudio(GlobalVariables.AudioManager, GlobalVariables.Instance.SongSpeed);
                    SongLength = GlobalVariables.AudioManager.AudioLengthD;
                    GlobalVariables.AudioManager.SongEnd += OnAudioEnd;
                }
                catch (Exception ex)
                {
                    _loadState = LoadFailureState.Error;
                    _loadFailureMessage = "Failed to load audio!";
                    Debug.LogException(ex, this);
                }
            });

            if (_loadState != LoadFailureState.None) return;

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

            if (!GlobalVariables.AudioManager.HasStem(stem) || !_stemStates.TryGetValue(stem, out var state)) return;

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

            if (!GlobalVariables.AudioManager.HasStem(stem) || !_stemStates.TryGetValue(stem, out var state)) return;

            if (reverb)
            {
                state.ReverbCount++;
            }
            else
            {
                state.ReverbCount = Math.Max(0, state.ReverbCount - 1);
            }

            GlobalVariables.AudioManager.ApplyReverb(stem, state.ReverbCount > 0);
        }

        private void OnAudioEnd()
        {
            if (IsPractice)
            {
                PracticeManager.ResetPractice();
                // Audio is paused automatically at this point, so we need to start it again
                GlobalVariables.AudioManager.Play();
                return;
            }

            if (IsReplay)
            {
                Pause(false);
                return;
            }

            EndSong();
        }
    }
}
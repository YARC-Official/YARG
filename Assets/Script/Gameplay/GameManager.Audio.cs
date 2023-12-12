using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Helpers.Extensions;
using YARG.Settings;

namespace YARG.Gameplay
{
    public partial class GameManager
    {
        public class StemState
        {
            public int Total;
            public int Muted;

            public float GetVolumeLevel()
            {
                if (Total == 0) return 1f;
                return (float) (Total - Muted) / Total;
            }
        }

        private readonly Dictionary<SongStem, StemState> _stemStates = new();

        private async UniTask LoadAudio()
        {
            // The stem states are initialized in "CreatePlayers"
            _stemStates.Clear();

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
                state.Muted--;
            }

            GlobalVariables.AudioManager.SetStemVolume(stem, state.GetVolumeLevel());
        }

        private void OnAudioEnd()
        {
            if (IsPractice)
            {
                PracticeManager.ResetPractice();
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
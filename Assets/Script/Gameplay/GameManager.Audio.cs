using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay
{
    public partial class GameManager
    {
        private async UniTask LoadAudio()
        {
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

            if (_loadState != LoadFailureState.None)
                return;

            _songLoaded?.Invoke();
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
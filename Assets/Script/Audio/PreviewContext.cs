using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Settings;
using YARG.Song;

namespace YARG.Audio
{
    public class PreviewContext
    {
        public double PreviewStartTime { get; private set; }
        public double PreviewEndTime { get; private set; }

        public bool IsPlaying { get; private set; }

        private readonly IAudioManager _manager;

        private UniTask _loopTask;

        public PreviewContext(IAudioManager manager)
        {
            _manager = manager;
        }

        private async UniTask StartLooping(float volume, CancellationToken cancelToken)
        {
            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    _manager.SetPosition(PreviewStartTime);
                    _manager.FadeIn(volume);

                    await UniTask.WaitUntil(() => cancelToken.IsCancellationRequested || (_manager.IsPlaying &&
                        (_manager.CurrentPositionD >= PreviewEndTime ||
                            _manager.CurrentPositionD >= _manager.AudioLengthD)));

                    await _manager.FadeOut();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error while looping song preview!");
                Debug.LogException(e);
            }
        }

        public async UniTask PlayPreview(SongEntry song, float volume, CancellationToken cancelToken)
        {
            // Skip if preview shouldn't be played
            if (song == null || Mathf.Approximately(volume, 0f))
            {
                return;
            }

            // Previews must be cancelled before attempting to start a new one
            if (IsPlaying)
            {
                Debug.LogError(
                    $"Attempted to play a new preview without cancelling the previous! Song: {song.Artist} - {song.Name}");
                return;
            }

            IsPlaying = true;

            try
            {
                // Wait for a X milliseconds to prevent spam loading (no one likes Music Library lag)
                await UniTask.Delay(TimeSpan.FromMilliseconds(300.0), ignoreTimeScale: true);

                // Check if cancelled
                if (cancelToken.IsCancellationRequested)
                {
                    return;
                }

                // Load the song
                bool usesPreviewFile = await song.LoadPreviewAudio(_manager, 1f);

                // Check if cancelled
                if (cancelToken.IsCancellationRequested)
                {
                    _manager.UnloadSong();
                    return;
                }

                double audioLength = _manager.AudioLengthD;
                if (!usesPreviewFile)
                {
                    // Set preview start and end times
                    PreviewStartTime = song.PreviewStartTimeSpan.TotalSeconds;
                    if (PreviewStartTime <= 0.0 || PreviewStartTime >= audioLength)
                    {
                        if (20 <= audioLength)
                            PreviewStartTime = 10;
                        else
                            PreviewStartTime = audioLength / 2;
                    }

                    PreviewEndTime = song.PreviewEndTimeSpan.TotalSeconds;
                    if (PreviewEndTime <= 0.0 || PreviewEndTime + 1 >= audioLength)
                    {
                        PreviewEndTime = PreviewStartTime + Constants.PREVIEW_DURATION;
                        if (PreviewEndTime + 1 > audioLength)
                            PreviewEndTime = audioLength - 1;
                    }
                }
                else
                {
                    PreviewStartTime = 0;
                    PreviewEndTime = audioLength - 1;
                }

                // Play the audio
                await StartLooping(volume, cancelToken);
            }
            catch (Exception ex)
            {
                Debug.LogError("Error while playing song preview!");
                Debug.LogException(ex);
            }
            finally
            {
                IsPlaying = false;
            }
        }
    }
}
﻿using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Helpers.Extensions;

namespace YARG.Audio
{
    public class PreviewContext
    {
        private const double DEFAULT_PREVIEW_DURATION = 30.0;

        public double PreviewStartTime { get; private set; }
        public double PreviewEndTime { get; private set; }

        public bool IsPlaying { get; private set; }

        private readonly IAudioManager _manager;

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

                    await UniTask.WaitUntil(() => cancelToken.IsCancellationRequested ||
                        !_manager.IsPlaying || _manager.CurrentPositionD >= PreviewEndTime);

                    await _manager.FadeOut();
                }
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Error while looping song preview!");
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
                YargLogger.LogFormatError("Attempted to play a new preview without cancelling the previous! Song: {0} - {1}", song.Artist, song.Name);
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
                bool usesPreviewFile = await song.LoadPreview(_manager, 1f);

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
                    PreviewStartTime = song.PreviewStartSeconds;
                    if (PreviewStartTime <= 0.0 || PreviewStartTime >= audioLength)
                    {
                        if (20 <= audioLength)
                            PreviewStartTime = 10;
                        else
                            PreviewStartTime = audioLength / 2;
                    }

                    PreviewEndTime = song.PreviewEndSeconds;
                    if (PreviewEndTime <= 0.0 || PreviewEndTime + 1 >= audioLength)
                    {
                        PreviewEndTime = PreviewStartTime + DEFAULT_PREVIEW_DURATION;
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
                YargLogger.LogException(ex, "Error while playing song preview!");
            }
            finally
            {
                IsPlaying = false;
            }
        }
    }
}
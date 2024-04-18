using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Menu.Persistent;
using YARG.Playback;
using YARG.Settings;

namespace YARG.GraphicsTest
{
    public partial class TestManager : MonoBehaviour
    {
        private enum LoadFailureState
        {
            None,
            Rescan,
            Error
        }

        private LoadFailureState _loadState;
        private string _loadFailureMessage;

        private async UniTask Load()
        {
            using var context = new LoadingContext();

            // Disable until everything's loaded
            enabled = false;

            YargLogger.LogFormatInfo("Loading song {0} - {1}", _song.Name, _song.Artist);

            context.Queue(UniTask.RunOnThreadPool(LoadChart), "Loading chart...");
            context.Queue(UniTask.RunOnThreadPool(LoadAudio), "Loading audio...");
            await context.Wait();

            if (_loadState == LoadFailureState.Rescan)
            {
                ToastManager.ToastWarning("Chart requires a rescan!");

                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                return;
            }

            if (_loadState == LoadFailureState.Error)
            {
                YargLogger.LogError(_loadFailureMessage);
                ToastManager.ToastError(_loadFailureMessage);

                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                return;
            }

            FinalizeChart();

            // Initialize song runner
            _songRunner = new SongRunner(
                _mixer,
                GlobalVariables.State.SongSpeed,
                SettingsManager.Settings.AudioCalibration.Value,
                SettingsManager.Settings.VideoCalibration.Value,
                _song.SongOffsetSeconds);

            // Log constant values
            YargLogger.LogFormatDebug("Audio calibration: {0}, video calibration: {1}, song offset: {2}",
                _songRunner.AudioCalibration, _songRunner.VideoCalibration, _songRunner.SongOffset);

            // Loaded, enable updates
            enabled = true;
        }

        private void LoadAudio()
        {
            _mixer = _song.LoadAudio(GlobalVariables.State.SongSpeed, DEFAULT_VOLUME);
            if (_mixer == null)
            {
                _loadState = LoadFailureState.Error;
                _loadFailureMessage = "Failed to load audio!";
                return;
            }
        }

        private void LoadChart()
        {
            try
            {
                _chart = _song.LoadChart();
                if (_chart == null)
                {
                    _loadState = LoadFailureState.Rescan;
                }
            }
            catch (Exception ex)
            {
                _loadState = LoadFailureState.Error;
                _loadFailureMessage = "Failed to load chart!";
                YargLogger.LogException(ex);
            }
        }

        private void FinalizeChart()
        {
            double audioLength = _mixer.Length;
            double chartLength = _chart.GetEndTime();
            double endTime = _chart.GetEndEvent()?.Time ?? -1;

            // - Chart < Audio < [end] -> Audio
            // - Chart < [end] < Audio -> [end]
            // - [end] < Chart < Audio -> Audio
            // - Audio < Chart         -> Chart
            if (audioLength <= chartLength)
            {
                _songLength = chartLength;
            }
            else if (endTime <= chartLength || audioLength <= endTime)
            {
                _songLength = audioLength;
            }
            else
            {
                _songLength = endTime;
            }
        }
    }
}
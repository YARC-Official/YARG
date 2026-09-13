using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Logging;
using YARG.Settings;

using YARG.Gameplay;
using YARG.Menu.Persistent;
using YARG.Settings.Types;

namespace YARG.Helpers
{
    public class AutoCalibrator : IDisposable
    {
        // Number of notes to collect before each adjustment
        private const int SAMPLE_SIZE = 20;

        // Fraction of the measured error to apply as correction (0-1).
        private const double DAMPING = 0.5;

        // Median error (ms) below which calibration is considered stable.
        private const double STABLE_THRESHOLD_MS = 5.0;

        private readonly List<double> _accuracyList = new();
        private readonly GameManager _gameManager;

        private int _calibration;
        private bool _isApplyingAdjustment;

        private bool IsCalibratingAudio => CalibrationMode == CalibrationType.AUDIO;
        private bool IsCalibratingVideo => CalibrationMode == CalibrationType.VIDEO;
        private bool IsCalibratingOffset => CalibrationMode == CalibrationType.OFFSET;
        private IntSetting AudioCalibrationSetting => SettingsManager.Settings.AudioCalibration;
        private IntSetting VideoCalibrationSetting => SettingsManager.Settings.VideoCalibration;
        private ToggleSetting AutoAudioSetting => SettingsManager.Settings.AutoCalibrateAudio;
        private ToggleSetting AutoVideoSetting => SettingsManager.Settings.AutoCalibrateVideo;
        private ToggleSetting AutoOffsetSetting => SettingsManager.Settings.AutoCalibrateOffset;

        // The current song's specific offset. Set once per song load, before players (and
        // therefore this calibrator) are created.
        private readonly IntSetting _songOffsetSetting;

        private enum CalibrationType
        {
            DISABLED,
            AUDIO,
            VIDEO,
            OFFSET
        }

        private CalibrationType CalibrationMode =>
            AutoAudioSetting.Value    ? CalibrationType.AUDIO
            : AutoVideoSetting.Value  ? CalibrationType.VIDEO
            : AutoOffsetSetting.Value ? CalibrationType.OFFSET
                                       : CalibrationType.DISABLED;

        public AutoCalibrator(GameManager gameManager)
        {
            _gameManager = gameManager;
            _songOffsetSetting = gameManager.SongOffsetOverride;

            AutoAudioSetting.OnChange += OnAutoCalibrateAudioChanged;
            AutoVideoSetting.OnChange += OnAutoCalibrateVideoChanged;
            AutoOffsetSetting.OnChange += OnAutoCalibrateOffsetChanged;
            AudioCalibrationSetting.OnChange += OnAudioCalibrationChanged;
            VideoCalibrationSetting.OnChange += OnVideoCalibrationChanged;
            if (_songOffsetSetting != null)
            {
                _songOffsetSetting.OnChange += OnSongOffsetChanged;
            }
            Reset();
        }

        private void OnAutoCalibrateAudioChanged(bool enabled)
        {
            if (enabled)
            {
                _gameManager.InvalidateScores("Menu.Toast.AutoCalibrationScore");
                AutoVideoSetting.Value = false;
                AutoOffsetSetting.Value = false;
            }

            Reset();
        }

        private void OnAutoCalibrateVideoChanged(bool enabled)
        {
            if (enabled)
            {
                _gameManager.InvalidateScores("Menu.Toast.AutoCalibrationScore");
                AutoAudioSetting.Value = false;
                AutoOffsetSetting.Value = false;
            }

            Reset();
        }

        private void OnAutoCalibrateOffsetChanged(bool enabled)
        {
            if (enabled)
            {
                _gameManager.InvalidateScores("Menu.Toast.AutoCalibrationScore");
                AutoAudioSetting.Value = false;
                AutoVideoSetting.Value = false;
            }

            Reset();
        }

        private void OnAudioCalibrationChanged(int calibration)
        {
            if (IsCalibratingAudio && !_isApplyingAdjustment)
            {
                _accuracyList.Clear();
                _calibration = calibration;
            }
        }

        private void OnVideoCalibrationChanged(int calibration)
        {
            if (IsCalibratingVideo && !_isApplyingAdjustment)
            {
                _accuracyList.Clear();
                _calibration = calibration;
            }
        }

        private void OnSongOffsetChanged(int calibration)
        {
            if (IsCalibratingOffset && !_isApplyingAdjustment)
            {
                _accuracyList.Clear();
                _calibration = calibration;
            }
        }

        public void Dispose()
        {
            AutoAudioSetting.OnChange -= OnAutoCalibrateAudioChanged;
            AutoVideoSetting.OnChange -= OnAutoCalibrateVideoChanged;
            AutoOffsetSetting.OnChange -= OnAutoCalibrateOffsetChanged;
            AudioCalibrationSetting.OnChange -= OnAudioCalibrationChanged;
            VideoCalibrationSetting.OnChange -= OnVideoCalibrationChanged;
            if (_songOffsetSetting != null)
            {
                _songOffsetSetting.OnChange -= OnSongOffsetChanged;
            }
        }

        private void Reset()
        {
            _accuracyList.Clear();
            if (IsCalibratingAudio)
            {
                _calibration = AudioCalibrationSetting.Value;
            }
            else if (IsCalibratingVideo)
            {
                _calibration = VideoCalibrationSetting.Value;
            }
            else if (IsCalibratingOffset && _songOffsetSetting != null)
            {
                _calibration = _songOffsetSetting.Value;
            }
        }

        public void RecordAccuracy(double hitTime, double noteTime)
        {
            if (CalibrationMode == CalibrationType.DISABLED || _gameManager.IsAudioSyncCorrectionActive)
            {
                return;
            }

            if (IsCalibratingOffset && _songOffsetSetting == null)
            {
                return;
            }

            double accuracy = (hitTime - noteTime) * 1000;
            _accuracyList.Add(accuracy);

            if (_accuracyList.Count < SAMPLE_SIZE)
            {
                return;
            }

            var filtered = RemoveOutliers(_accuracyList);
            double median = CalculateMedian(filtered);
            int adjustment = (int) Math.Round(median * DAMPING);

            if (Math.Abs(median) <= STABLE_THRESHOLD_MS)
            {
                NotifyCalibrationStable();
            }
            else
            {
                ApplyAdjustment(adjustment);
                NotifyCalibrationUpdated();
            }

            _accuracyList.Clear();
        }

        private void ApplyAdjustment(int adjustment)
        {
            _calibration += adjustment;
            _isApplyingAdjustment = true;
            try
            {
                if (CalibrationMode == CalibrationType.AUDIO)
                {
                    AudioCalibrationSetting.Value = _calibration;
                    _calibration = AudioCalibrationSetting.Value;
                }
                else if (CalibrationMode == CalibrationType.VIDEO)
                {
                    VideoCalibrationSetting.Value = _calibration;
                    _calibration = VideoCalibrationSetting.Value;
                }
                else if (CalibrationMode == CalibrationType.OFFSET && _songOffsetSetting != null)
                {
                    _songOffsetSetting.Value = _calibration;
                    _calibration = _songOffsetSetting.Value;
                }
            }
            finally
            {
                _isApplyingAdjustment = false;
            }

            _gameManager.UpdateCalibration();
        }

        private static string CalibrationTypeName(CalibrationType type) => type switch
        {
            CalibrationType.AUDIO  => "Audio",
            CalibrationType.VIDEO  => "Video",
            CalibrationType.OFFSET => "Song offset",
            _                      => "Calibration"
        };

        private void NotifyCalibrationUpdated()
        {
            var type = CalibrationTypeName(CalibrationMode);
            ToastManager.ToastMessage($"{type} calibration updated: {_calibration} ms");
        }

        private void NotifyCalibrationStable()
        {
            var type = CalibrationTypeName(CalibrationMode);
            ToastManager.ToastSuccess($"{type} calibration stable ({_calibration} ms)");
        }

        // Removes hits near the edges of the hit window.  We find the "middle 50%" of the data (Q1 to Q3),
        // measure how spread out that last range is (IQR), then toss anything more than 1.5x of the range.
        private static List<double> RemoveOutliers(List<double> values)
        {
            var sorted = values.OrderBy(x => x).ToList();
            int count = sorted.Count;

            double q1 = sorted[count / 4];
            double q3 = sorted[count * 3 / 4];
            double iqr = q3 - q1;

            double lowerBound = q1 - 1.5 * iqr;
            double upperBound = q3 + 1.5 * iqr;

            return sorted.Where(x => x >= lowerBound && x <= upperBound).ToList();
        }

        private static double CalculateMedian(List<double> values)
        {
            var sortedValues = values.OrderBy(x => x).ToList();
            int count = sortedValues.Count;
            int middleIndex = count / 2;
            if (count % 2 == 0)
            {
                return (sortedValues[middleIndex - 1] + sortedValues[middleIndex]) / 2.0;
            }
            return sortedValues[middleIndex];
        }
    }
}

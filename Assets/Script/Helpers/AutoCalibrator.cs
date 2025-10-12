using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Logging;
using YARG.Settings;
using YARG.Core.Audio;
using YARG.Gameplay;
using YARG.Menu.Persistent;

namespace YARG.Helpers
{
    public class AutoCalibrator
    {
        private const int SAMPLE_SIZE = 20;
        private const double MAX_CALIBRATION_CHANGE = 20.0;
        private const double STABLE_THRESHOLD = 10.0;

        private readonly List<double> _accuracyList = new();
        private readonly GameManager _gameManager;

        private int _baselineCalibration;
        private double? _lastAbsMedian;
        private bool _isStable;

        public AutoCalibrator(GameManager gameManager)
        {
            _gameManager = gameManager;
            _baselineCalibration = SettingsManager.Settings.AudioCalibration.Value;
        }

        public void RecordAccuracy(double noteTime)
        {
            double accuracy = (_gameManager.InputTime - noteTime) * 1000;
            _accuracyList.Add(accuracy);
            if (_accuracyList.Count < SAMPLE_SIZE)
            {
                return;
            }

            double median = CalculateMedian(_accuracyList);
            double absMedian = Math.Abs(median);
            if (absMedian < STABLE_THRESHOLD)
            {
                _isStable = true;
            }

            bool shouldUpdate = !_isStable || !_lastAbsMedian.HasValue || absMedian < _lastAbsMedian.Value;
            if (shouldUpdate)
            {
                UpdateCalibration(median, absMedian);
            }
            else
            {
                NotifyCalibrationStable();
            }
            _accuracyList.Clear();
        }

        private void UpdateCalibration(double median, double absMedian)
        {
            int clampedMedian = (int) Math.Clamp(median, -MAX_CALIBRATION_CHANGE, MAX_CALIBRATION_CHANGE);
            var hardwareAdjustment = SettingsManager.Settings.AccountForHardwareLatency.Value ? GlobalAudioHandler.PlaybackLatency : 0;
            int calibrationValue = _baselineCalibration + clampedMedian - hardwareAdjustment;
            SettingsManager.Settings.AudioCalibration.Value = calibrationValue;
            _gameManager.UpdateCalibration(); //Hacky

            ToastManager.ToastMessage($"Calibration updated: {calibrationValue} ms");
            _baselineCalibration = calibrationValue;
            _lastAbsMedian = absMedian;
        }

        private void NotifyCalibrationStable()
        {
            YargLogger.LogInfo($"Calibration stable ({_baselineCalibration} ms)");
            ToastManager.ToastSuccess($"Calibration stable ({_baselineCalibration} ms)");
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
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
        // Number of notes for the initial (coarse) adjustment
        private const int INITIAL_SAMPLE_SIZE = 10;

        // Number of notes for subsequent (fine-tuning) adjustments
        private const int SAMPLE_SIZE = 20;

        // Median accuracy (in ms) below which calibration is considered stable
        private const double STABLE_THRESHOLD_MS = 10.0;

        private readonly List<double> _accuracyList = new();
        private readonly GameManager _gameManager;

        private int _calibration;
        private bool _isFirstAdjustment = true;

        public AutoCalibrator(GameManager gameManager)
        {
            _gameManager = gameManager;

            int calibration = SettingsManager.Settings.AudioCalibration.Value;
            if (SettingsManager.Settings.AccountForHardwareLatency.Value)
            {
                calibration -= GlobalAudioHandler.PlaybackLatency;
            }
            _calibration = calibration;
        }

        public void RecordAccuracy(double noteTime)
        {

            double accuracy = (_gameManager.InputTime - noteTime) * 1000;
            _accuracyList.Add(accuracy);

            int requiredSamples = _isFirstAdjustment ? INITIAL_SAMPLE_SIZE : SAMPLE_SIZE;
            if (_accuracyList.Count < requiredSamples)
            {
                return;
            }

            double median = CalculateMedian(_accuracyList);
            double absMedian = Math.Abs(median);
            UpdateCalibration(median);

            if (absMedian < STABLE_THRESHOLD_MS)
            {
                NotifyCalibrationStable();
            }
            else
            {
                NotifyCalibrationUpdated();
            }

            _isFirstAdjustment = false;
            _accuracyList.Clear();
        }

        private void UpdateCalibration(double median)
        {
            int newCalibration = _calibration + (int) median;
            UpdateCalibrationSetting(newCalibration);
            _gameManager.UpdateCalibration();
            _calibration = newCalibration;
        }

        private void UpdateCalibrationSetting(int calibrationValue)
        {
            SettingsManager.Settings.AudioCalibration.Value = calibrationValue;
        }

        private void NotifyCalibrationUpdated()
        {
            ToastManager.ToastMessage($"Calibration updated: {_calibration} ms");
        }

        private void NotifyCalibrationStable()
        {
            ToastManager.ToastSuccess($"Auto calibration stable ({_calibration} ms)");
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

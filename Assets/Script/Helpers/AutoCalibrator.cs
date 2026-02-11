using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Logging;
using YARG.Settings;

using YARG.Gameplay;
using YARG.Menu.Persistent;

namespace YARG.Helpers
{
    public class AutoCalibrator
    {
        // Number of notes to collect before each adjustment
        private const int SAMPLE_SIZE = 20;

        // Fraction of the measured error to apply as correction (0-1).
        private const double DAMPING = 0.5;

        private readonly List<double> _accuracyList = new();
        private readonly GameManager _gameManager;

        private int _calibration;

        public AutoCalibrator(GameManager gameManager)
        {
            _gameManager = gameManager;
            _calibration = SettingsManager.Settings.AudioCalibration.Value;
        }

        public void RecordAccuracy(double noteTime)
        {

            double accuracy = (_gameManager.InputTime - noteTime) * 1000;
            _accuracyList.Add(accuracy);

            if (_accuracyList.Count < SAMPLE_SIZE)
            {
                return;
            }

            var filtered = RemoveOutliers(_accuracyList);
            double median = CalculateMedian(filtered);
            int adjustment = (int) Math.Round(median * DAMPING);

            if (adjustment == 0)
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
            SettingsManager.Settings.AudioCalibration.Value = _calibration;
            _gameManager.UpdateCalibration();
        }

        private void NotifyCalibrationUpdated()
        {
            ToastManager.ToastMessage($"Calibration updated: {_calibration} ms");
        }

        private void NotifyCalibrationStable()
        {
            ToastManager.ToastSuccess($"Auto calibration stable ({_calibration} ms)");
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

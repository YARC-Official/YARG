using System;
using System.Collections.Generic;
using System.Linq;

namespace YARG.Helpers
{
    public partial class AutoCalibrator
    {
        // Number of notes to collect before each adjustment
        public const int SAMPLE_SIZE = 20;

        // Fraction of the measured error to apply as correction (0-1).
        public const double DAMPING = 0.5;

        // Median error (ms) below which calibration is considered stable.
        public const double STABLE_THRESHOLD_MS = 5.0;

        public static int CalculateAdjustment(double median) => (int) Math.Round(median * DAMPING);

        public static bool IsStable(double median) => Math.Abs(median) <= STABLE_THRESHOLD_MS;

        // Removes hits near the edges of the hit window.  We find the "middle 50%" of the data (Q1 to Q3),
        // measure how spread out that last range is (IQR), then toss anything more than 1.5x of the range.
        public static List<double> RemoveOutliers(IReadOnlyList<double> values)
        {
            if (values.Count < 4)
            {
                return values.ToList();
            }

            var sorted = values.OrderBy(x => x).ToList();
            int count = sorted.Count;

            double q1 = sorted[count / 4];
            double q3 = sorted[count * 3 / 4];
            double iqr = q3 - q1;

            double lowerBound = q1 - 1.5 * iqr;
            double upperBound = q3 + 1.5 * iqr;

            return sorted.Where(x => x >= lowerBound && x <= upperBound).ToList();
        }

        public static double CalculateMedian(IReadOnlyList<double> values)
        {
            if (values.Count == 0)
            {
                return 0.0;
            }

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

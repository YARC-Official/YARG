using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shouldly;
using YARG.Helpers;

namespace YARG.UnitTests
{
    public sealed class AutoCalibratorTests
    {
        [Test]
        public void CalculateMedian_EmptyCollection_ReturnsZero()
        {
            var values = Array.Empty<double>();
            AutoCalibrator.CalculateMedian(values).ShouldBe(0.0);
        }

        [Test]
        public void CalculateMedian_OddCount_ReturnsMiddleElement()
        {
            var values = new[] { 5.0, 1.0, 9.0 };
            AutoCalibrator.CalculateMedian(values).ShouldBe(5.0);
        }

        [Test]
        public void CalculateMedian_EvenCount_ReturnsAverageOfMiddleElements()
        {
            var values = new[] { 10.0, 20.0, 30.0, 40.0 };
            AutoCalibrator.CalculateMedian(values).ShouldBe(25.0);
        }

        [Test]
        public void CalculateMedian_SingleElement_ReturnsElement()
        {
            var values = new[] { 42.0 };
            AutoCalibrator.CalculateMedian(values).ShouldBe(42.0);
        }

        [Test]
        public void CalculateMedian_NegativeAndUnsorted_ReturnsCorrectMedian()
        {
            var values = new[] { 12.0, -8.0, 0.0, 4.0, -2.0 };
            AutoCalibrator.CalculateMedian(values).ShouldBe(0.0);
        }

        [Test]
        public void RemoveOutliers_CountLessThanFour_ReturnsAllElements()
        {
            var values = new[] { 10.0, 200.0, -50.0 };
            var result = AutoCalibrator.RemoveOutliers(values);
            result.Count.ShouldBe(3);
        }

        [Test]
        public void RemoveOutliers_ExtremeValues_FiltersOutliersBeyondIQR()
        {
            var values = new List<double>();
            for (var i = 0; i < 18; i++)
            {
                values.Add(10.0);
            }
            values.Add(-500.0);
            values.Add(500.0);

            var filtered = AutoCalibrator.RemoveOutliers(values);

            filtered.Count.ShouldBe(18);
            filtered.ShouldAllBe(x => x == 10.0);
        }

        [Test]
        public void RemoveOutliers_UniformSpread_KeepsNormalValues()
        {
            var values = new List<double>();
            for (var i = 10; i < 30; i++)
            {
                values.Add(i);
            }

            var filtered = AutoCalibrator.RemoveOutliers(values);

            filtered.Count.ShouldBe(values.Count);
        }

        [TestCase(0.0, ExpectedResult = 0)]
        [TestCase(10.0, ExpectedResult = 5)]
        [TestCase(-20.0, ExpectedResult = -10)]
        [TestCase(15.0, ExpectedResult = 8)]
        [TestCase(-15.0, ExpectedResult = -8)]
        public int CalculateAdjustment_AppliesDampingAndRounds(double median)
        {
            return AutoCalibrator.CalculateAdjustment(median);
        }

        [TestCase(0.0, ExpectedResult = true)]
        [TestCase(5.0, ExpectedResult = true)]
        [TestCase(-5.0, ExpectedResult = true)]
        [TestCase(4.99, ExpectedResult = true)]
        [TestCase(5.01, ExpectedResult = false)]
        [TestCase(-5.01, ExpectedResult = false)]
        [TestCase(25.0, ExpectedResult = false)]
        public bool IsStable_ThresholdBoundaryCheck(double median)
        {
            return AutoCalibrator.IsStable(median);
        }

        [Test]
        public void Simulation_FixedLatencyOffset_ConvergesMonotonicallyToStability()
        {
            const double TRUE_HARDWARE_LATENCY = 60.0;
            var currentCalibration = 0;
            var isStable = false;
            var batchCount = 0;

            while (!isStable && batchCount < 10)
            {
                batchCount++;
                var samples = new List<double>(AutoCalibrator.SAMPLE_SIZE);
                for (var i = 0; i < AutoCalibrator.SAMPLE_SIZE; i++)
                {
                    var jitter = (i % 5) - 2;
                    var measuredError = (TRUE_HARDWARE_LATENCY - currentCalibration) + jitter;
                    samples.Add(measuredError);
                }

                var filtered = AutoCalibrator.RemoveOutliers(samples);
                var median = AutoCalibrator.CalculateMedian(filtered);
                isStable = AutoCalibrator.IsStable(median);

                if (!isStable)
                {
                    var adjustment = AutoCalibrator.CalculateAdjustment(median);
                    currentCalibration += adjustment;
                }
            }

            isStable.ShouldBeTrue();
            batchCount.ShouldBeLessThan(8);
            Math.Abs(TRUE_HARDWARE_LATENCY - currentCalibration).ShouldBeLessThanOrEqualTo(AutoCalibrator.STABLE_THRESHOLD_MS);
        }
    }
}

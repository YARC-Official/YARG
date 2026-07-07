#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace YARG.Playback
{
    public sealed class SyncCorrectionCalculatorTests
    {
        private const double STREAM_DELAY_MS = 100.0;
        private const double ELAPSED_MS = 10.0;
        private const double TOLERANCE = 0.000001;

        [Test]
        public void CalculateAdjustment_WhenWithinDeadband_ReturnsZero()
        {
            var calculator = new SyncCorrectionCalculator();

            float adjustment = calculator.CalculateAdjustment(0.001, ELAPSED_MS, STREAM_DELAY_MS, false);

            Assert.That(adjustment, Is.EqualTo(0f).Within(TOLERANCE));
        }

        [Test]
        public void CalculateAdjustment_WhenPositiveErrorIsLarge_ClampsPositive()
        {
            var calculator = new SyncCorrectionCalculator();

            float adjustment = calculator.CalculateAdjustment(1.0, ELAPSED_MS, STREAM_DELAY_MS, false);

            Assert.That(adjustment, Is.EqualTo(0.1f).Within(TOLERANCE));
        }

        [Test]
        public void CalculateAdjustment_WhenNegativeErrorIsLarge_ClampsNegative()
        {
            var calculator = new SyncCorrectionCalculator();

            float adjustment = calculator.CalculateAdjustment(-1.0, ELAPSED_MS, STREAM_DELAY_MS, false);

            Assert.That(adjustment, Is.EqualTo(-0.1f).Within(TOLERANCE));
        }

        [Test]
        public void CalculateAdjustment_WhenSuppressed_ReturnsZero()
        {
            var calculator = new SyncCorrectionCalculator();

            float adjustment = calculator.CalculateAdjustment(1.0, ELAPSED_MS, STREAM_DELAY_MS, true);

            Assert.That(adjustment, Is.EqualTo(0f).Within(TOLERANCE));
        }

        [Test]
        public void CalculateAdjustment_CompensatesForRecentHistory()
        {
            var calculator = new SyncCorrectionCalculator();
            calculator.CalculateAdjustment(1.0, 50.0, STREAM_DELAY_MS, false);

            float adjustment = calculator.CalculateAdjustment(0.010, ELAPSED_MS, STREAM_DELAY_MS, false);

            Assert.That(adjustment, Is.EqualTo(0.015f).Within(TOLERANCE));
        }

        [Test]
        public void CalculateAdjustment_SuppressedZeroAdjustment_AgesOutHistory()
        {
            var calculator = new SyncCorrectionCalculator();
            calculator.CalculateAdjustment(1.0, STREAM_DELAY_MS, STREAM_DELAY_MS, false);
            calculator.CalculateAdjustment(1.0, STREAM_DELAY_MS, STREAM_DELAY_MS, true);

            float adjustment = calculator.CalculateAdjustment(0.010, ELAPSED_MS, STREAM_DELAY_MS, false);

            Assert.That(adjustment, Is.EqualTo(0.03f).Within(TOLERANCE));
        }

        [Test]
        public void CalculateAdjustment_WhenHistoryExceedsWindow_RemovesOldEntries()
        {
            var calculator = new SyncCorrectionCalculator();
            calculator.CalculateAdjustment(1.0, 100.0, STREAM_DELAY_MS, false);
            calculator.CalculateAdjustment(1.0, 100.0, STREAM_DELAY_MS, true);

            float adjustment = calculator.CalculateAdjustment(0.010, ELAPSED_MS, STREAM_DELAY_MS, false);

            Assert.That(adjustment, Is.EqualTo(0.03f).Within(TOLERANCE));
        }

        [Test]
        public void CalculateAdjustment_WhenOldestEntryPartiallyOverlapsWindow_TrimsProportionally()
        {
            var calculator = new SyncCorrectionCalculator();
            calculator.CalculateAdjustment(1.0, 80.0, STREAM_DELAY_MS, false);
            calculator.CalculateAdjustment(0.0246666666667, 80.0, STREAM_DELAY_MS, false);

            float adjustment = calculator.CalculateAdjustment(0.006, ELAPSED_MS, STREAM_DELAY_MS, false);

            Assert.That(adjustment, Is.EqualTo(0f).Within(0.000001f));
        }

        [Test]
        public void Reset_ClearsHistoryCompensation()
        {
            var calculator = new SyncCorrectionCalculator();
            calculator.CalculateAdjustment(1.0, 50.0, STREAM_DELAY_MS, false);

            calculator.Reset();
            float adjustment = calculator.CalculateAdjustment(0.010, ELAPSED_MS, STREAM_DELAY_MS, false);

            Assert.That(adjustment, Is.EqualTo(0.03f).Within(TOLERANCE));
        }
    }
}
#endif

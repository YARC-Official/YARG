using System;

namespace YARG.Audio.PitchDetection
{
    class PitchProcessor
    {
        const int kCourseOctaveSteps = 96;
        const int kScanHiSize = 31;
        const float kScanHiFreqStep = 1.005f;

        readonly int m_blockLen44; // 4/4 block len

        readonly float m_detectLevelThreshold;

        readonly int m_numCourseSteps;
        readonly float[] m_pCourseFreqOffset;
        readonly float[] m_pCourseFreq;
        readonly float[] m_scanHiOffset = new float[kScanHiSize];
        readonly float[] m_peakBuf = new float[kScanHiSize];
        int m_prevPitchIdx;
        readonly float[] m_detectCurve;

        public PitchProcessor(double SampleRate, float MinPitch, float MaxPitch, float DetectLevelThreshold)
        {
            m_detectLevelThreshold = DetectLevelThreshold;

            m_blockLen44 = (int) (SampleRate / MinPitch + 0.5);

            m_numCourseSteps =
                (int) (Math.Log((double) MaxPitch / MinPitch) / Math.Log(2.0) * kCourseOctaveSteps + 0.5) + 3;

            m_pCourseFreqOffset = new float[m_numCourseSteps + 10000];
            m_pCourseFreq = new float[m_numCourseSteps + 10000];

            m_detectCurve = new float[m_numCourseSteps];

            var freqStep = 1 / Math.Pow(2.0, 1.0 / kCourseOctaveSteps);
            var curFreq = MaxPitch / freqStep;

            // frequency is stored from high to low
            for (var i = 0; i < m_numCourseSteps; i++)
            {
                m_pCourseFreq[i] = (float) curFreq;
                m_pCourseFreqOffset[i] = (float) (SampleRate / curFreq);
                curFreq *= freqStep;
            }

            for (var i = 0; i < kScanHiSize; i++)
                m_scanHiOffset[i] = (float) Math.Pow(kScanHiFreqStep, kScanHiSize / 2 - i);
        }

        /// <summary>
        /// Detect the pitch
        /// </summary>
        public float DetectPitch(float[] samplesLo, float[] samplesHi, int numSamples)
        {
            // Level is too low
            if (!LevelIsAbove(samplesLo, numSamples, m_detectLevelThreshold) &&
                !LevelIsAbove(samplesHi, numSamples, m_detectLevelThreshold))
                return 0;

            return DetectPitchLo(samplesLo, samplesHi);
        }

        /// <summary>
        /// Low resolution pitch detection
        /// </summary>
        float DetectPitchLo(float[] samplesLo, float[] samplesHi)
        {
            Array.Clear(m_detectCurve, 0, m_detectCurve.Length);

            const int skipSize = 8, peakScanSize = 23, peakScanSizeHalf = peakScanSize / 2;

            const float peakThresh1 = 200.0f, peakThresh2 = 600.0f;
            var bufferSwitched = false;

            for (var idx = 0; idx < m_numCourseSteps; idx += skipSize)
            {
                var blockLen = Math.Min(m_blockLen44, (int) m_pCourseFreqOffset[idx] * 2);
                float[] curSamples;

                // 258 is at 250 Hz, which is the switchover frequency for the two filters
                var loBuffer = idx >= 258;

                if (loBuffer)
                {
                    if (!bufferSwitched)
                    {
                        Array.Clear(m_detectCurve, 258 - peakScanSizeHalf, peakScanSizeHalf + peakScanSizeHalf + 1);
                        bufferSwitched = true;
                    }

                    curSamples = samplesLo;
                }
                else
                {
                    curSamples = samplesHi;
                }

                var stepSizeLoRes = blockLen / 10;
                var stepSizeHiRes = Math.Max(1, Math.Min(5, idx * 5 / m_numCourseSteps));

                var fValue = RatioAbsDiffLinear(curSamples, idx, blockLen, stepSizeLoRes, false);

                if (!(fValue > peakThresh1)) continue;

                // Do a closer search for the peak
                var peakIdx = -1;
                var peakVal = 0.0f;
                var prevVal = 0.0f;
                var dir = 4;      // start going forward
                var curPos = idx; // start at center of the scan range
                var begSearch = Math.Max(idx - peakScanSizeHalf, 0);
                var endSearch = Math.Min(idx + peakScanSizeHalf, m_numCourseSteps - 1);

                while (curPos >= begSearch && curPos < endSearch)
                {
                    var curVal = RatioAbsDiffLinear(curSamples, curPos, blockLen, stepSizeHiRes, true);

                    if (peakVal < curVal)
                    {
                        peakVal = curVal;
                        peakIdx = curPos;
                    }

                    if (prevVal > curVal)
                    {
                        dir = -dir >> 1;

                        if (dir == 0)
                        {
                            if (peakVal > peakThresh2 && peakIdx >= 6 && peakIdx <= m_numCourseSteps - 7)
                            {
                                var fValL = RatioAbsDiffLinear(curSamples, peakIdx - 5, blockLen, stepSizeHiRes, true);
                                var fValR = RatioAbsDiffLinear(curSamples, peakIdx + 5, blockLen, stepSizeHiRes, true);
                                var fPointy = peakVal / (fValL + fValR) * 2.0f;

                                var minPointy = m_prevPitchIdx > 0 && Math.Abs(m_prevPitchIdx - peakIdx) < 10
                                    ? 1.2f
                                    : 1.5f;

                                if (fPointy > minPointy)
                                {
                                    var pitchHi = DetectPitchHi(curSamples, peakIdx);

                                    if (pitchHi > 1.0f)
                                    {
                                        m_prevPitchIdx = peakIdx;
                                        return pitchHi;
                                    }
                                }
                            }

                            break;
                        }
                    }

                    prevVal = curVal;
                    curPos += dir;
                }
            }

            m_prevPitchIdx = 0;
            return 0;
        }

        /// <summary>
        /// High resolution pitch detection
        /// </summary>
        float DetectPitchHi(float[] samples, int lowFreqIdx)
        {
            var peakIdx = -1;
            var prevVal = 0.0f;
            var dir = 4;                   // start going forward
            var curPos = kScanHiSize >> 1; // start at center of the scan range

            Array.Clear(m_peakBuf, 0, m_peakBuf.Length);

            var offset = m_pCourseFreqOffset[lowFreqIdx];

            while (curPos >= 0 && curPos < kScanHiSize)
            {
                if (m_peakBuf[curPos] == 0)
                    m_peakBuf[curPos] = SumAbsDiffHermite(samples, offset * m_scanHiOffset[curPos], m_blockLen44, 1);

                if (peakIdx < 0 || m_peakBuf[peakIdx] < m_peakBuf[curPos]) peakIdx = curPos;

                if (prevVal > m_peakBuf[curPos])
                {
                    dir = -dir >> 1;

                    if (dir == 0)
                    {
                        // found the peak
                        var minVal = Math.Min(m_peakBuf[peakIdx - 1], m_peakBuf[peakIdx + 1]);

                        minVal -= minVal * (1.0f / 32.0f);

                        var y1 = (float) Math.Log10(m_peakBuf[peakIdx - 1] - minVal);
                        var y2 = (float) Math.Log10(m_peakBuf[peakIdx] - minVal);
                        var y3 = (float) Math.Log10(m_peakBuf[peakIdx + 1] - minVal);

                        var fIdx = peakIdx + (y3 - y1) / (2.0f * (2.0f * y2 - y1 - y3));

                        return (float) Math.Pow(kScanHiFreqStep, fIdx - kScanHiSize / 2.0) * m_pCourseFreq[lowFreqIdx];
                    }
                }

                prevVal = m_peakBuf[curPos];
                curPos += dir;
            }

            return 0;
        }

        /// <summary>
        /// Returns true if the level is above the specified value
        /// </summary>
        static bool LevelIsAbove(float[] buffer, int len, float level)
        {
            if (buffer == null || buffer.Length == 0) return false;

            var endIdx = Math.Min(buffer.Length, len);

            for (var idx = 0; idx < endIdx; idx++)
                if (Math.Abs(buffer[idx]) >= level)
                    return true;

            return false;
        }

        /// <summary>
        /// // 4-point, 3rd-order Hermite (x-form)
        /// </summary>
        static float InterpolateHermite(float fY0, float fY1, float fY2, float fY3, float frac)
        {
            var c1 = 0.5f * (fY2 - fY0);
            var c3 = 1.5f * (fY1 - fY2) + 0.5f * (fY3 - fY0);
            var c2 = fY0 - fY1 + c1 - c3;

            return ((c3 * frac + c2) * frac + c1) * frac + fY1;
        }

        /// <summary>
        /// Linear interpolation
        /// nFrac is based on 1.0 = 256
        /// </summary>
        static float InterpolateLinear(float y0, float y1, float frac) => y0 * (1.0f - frac) + y1 * frac;

        /// <summary>
        /// Medium Low res SumAbsDiff
        /// </summary>
        float RatioAbsDiffLinear(float[] samples, int freqIdx, int blockLen, int stepSize, bool hiRes)
        {
            if (hiRes && m_detectCurve[freqIdx] > 0.0f) return m_detectCurve[freqIdx];

            var offsetInt = (int) m_pCourseFreqOffset[freqIdx];
            var offsetFrac = m_pCourseFreqOffset[freqIdx] - offsetInt;
            var rect = 0.0f;
            var absDiff = 0.01f; // prevent divide by zero
            var count = 0;

            // Do a scan using linear interpolation and the specified step size
            for (var idx = 0; idx < blockLen; idx += stepSize, count++)
            {
                var sample = samples[idx];
                var interp = InterpolateLinear(samples[offsetInt + idx], samples[offsetInt + idx + 1], offsetFrac);
                absDiff += Math.Abs(sample - interp);
                rect += Math.Abs(sample) + Math.Abs(interp);
            }

            var finalVal = rect / absDiff * 100.0f;

            if (hiRes) m_detectCurve[freqIdx] = finalVal;

            return finalVal;
        }

        /// <summary>
        /// Medium High res SumAbsDiff
        /// </summary>
        static float SumAbsDiffHermite(float[] samples, float fOffset, int blockLen, int stepSize)
        {
            var offsetInt = (int) fOffset;
            var offsetFrac = fOffset - offsetInt;
            var value = 0.001f; // prevent divide by zero
            var count = 0;

            // do a scan using linear interpolation and the specified step size
            for (var idx = 0; idx < blockLen; idx += stepSize, count++)
            {
                var offsetIdx = offsetInt + idx;

                value += Math.Abs(samples[idx] - InterpolateHermite(samples[offsetIdx - 1], samples[offsetIdx],
                    samples[offsetIdx + 1], samples[offsetIdx + 2], offsetFrac));
            }

            return count / value;
        }
    }
}
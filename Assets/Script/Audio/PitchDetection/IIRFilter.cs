using System;
using System.Linq;

namespace YARG.Audio.PitchDetection
{
    enum IIRFilterType
    {
        LP,
        HP
    }

    /// <summary>
    /// Infinite impulse response filter (old style analog filters)
    /// </summary>
    class IIRFilter
    {
        const int kHistMask = 31, kHistSize = 32;

        int m_order;
        IIRFilterType m_filterType;

        float m_fp1, m_fp2, m_fN, m_sampleRate;
        double[] m_real, m_imag, m_z, m_aCoeff, m_bCoeff, m_inHistory, m_outHistory;
        int m_histIdx;
        bool m_invertDenormal;

        /// <summary>
        /// Returns true if all the filter parameters are valid
        /// </summary>
        public bool FilterValid
        {
            get
            {
                if (m_order < 1 || m_order > 16 ||
                    m_sampleRate <= 0 ||
                    m_fN <= 0)
                    return false;

                switch (m_filterType)
                {
                    case IIRFilterType.LP:
                        if (m_fp2 <= 0) return false;
                        break;

                    case IIRFilterType.HP:
                        if (m_fp1 <= 0) return false;
                        break;
                }

                return true;
            }
        }

        public float FreqLow
        {
            get => m_fp1;
            set
            {
                m_fp1 = value;
                Design();
            }
        }

        public float FreqHigh
        {
            get => m_fp2;
            set
            {
                m_fp2 = value;
                Design();
            }
        }

        public IIRFilter(IIRFilterType type, int order, float sampleRate)
        {
            m_filterType = type;

            m_order = Math.Min(16, Math.Max(1, Math.Abs(order)));

            m_sampleRate = sampleRate;
            m_fN = 0.5f * m_sampleRate;

            Design();
        }

        static bool IsOdd(int n) => (n & 1) == 1;

        static double Sqr(double value) => value * value;

        /// <summary>
        /// Determines poles and zeros of IIR filter
        /// based on bilinear transform method
        /// </summary>
        void LocatePolesAndZeros()
        {
            m_real = new double[m_order + 1];
            m_imag = new double[m_order + 1];
            m_z = new double[m_order + 1];

            // Butterworth, Chebyshev parameters
            var n = m_order;

            var ir = n % 2;
            var n1 = n + ir;
            var n2 = (3 * n + ir) / 2 - 1;
            double f1;

            switch (m_filterType)
            {
                case IIRFilterType.LP:
                    f1 = m_fp2;
                    break;

                case IIRFilterType.HP:
                    f1 = m_fN - m_fp1;
                    break;

                default:
                    f1 = 0.0;
                    break;
            }

            var tanw1 = Math.Tan(0.5 * Math.PI * f1 / m_fN);
            var tansqw1 = Sqr(tanw1);

            // Real and Imaginary parts of low-pass poles
            double r;

            for (var k = n1; k <= n2; k++)
            {
                var t = 0.5 * (2 * k + 1 - ir) * Math.PI / n;

                var b3 = 1.0 - 2.0 * tanw1 * Math.Cos(t) + tansqw1;
                r = (1.0 - tansqw1) / b3;
                var i = 2.0 * tanw1 * Math.Sin(t) / b3;

                var m = 2 * (n2 - k) + 1;
                m_real[m + ir] = r;
                m_imag[m + ir] = Math.Abs(i);
                m_real[m + ir + 1] = r;
                m_imag[m + ir + 1] = -Math.Abs(i);
            }

            if (IsOdd(n))
            {
                r = (1.0 - tansqw1) / (1.0 + 2.0 * tanw1 + tansqw1);

                m_real[1] = r;
                m_imag[1] = 0.0;
            }

            switch (m_filterType)
            {
                case IIRFilterType.LP:
                    for (var m = 1; m <= n; m++) m_z[m] = -1.0;
                    break;

                case IIRFilterType.HP:
                    // Low-pass to high-pass transformation
                    for (var m = 1; m <= n; m++)
                    {
                        m_real[m] = -m_real[m];
                        m_z[m] = 1.0;
                    }

                    break;
            }
        }

        /// <summary>
        /// Calculate all the values
        /// </summary>
        public void Design()
        {
            if (!FilterValid) return;

            m_aCoeff = new double[m_order + 1];
            m_bCoeff = new double[m_order + 1];
            m_inHistory = new double[kHistSize];
            m_outHistory = new double[kHistSize];

            var newA = new double[m_order + 1];
            var newB = new double[m_order + 1];

            // Find filter poles and zeros
            LocatePolesAndZeros();

            // Compute filter coefficients from pole/zero values
            m_aCoeff[0] = 1.0;
            m_bCoeff[0] = 1.0;

            for (var i = 1; i <= m_order; i++) m_aCoeff[i] = m_bCoeff[i] = 0.0;

            var k = 0;
            var n = m_order;
            var pairs = n / 2;

            if (IsOdd(m_order))
            {
                // First subfilter is first order
                m_aCoeff[1] = -m_z[1];
                m_bCoeff[1] = -m_real[1];
                k = 1;
            }

            for (var p = 1; p <= pairs; p++)
            {
                var m = 2 * p - 1 + k;
                var alpha1 = -(m_z[m] + m_z[m + 1]);
                var alpha2 = m_z[m] * m_z[m + 1];
                var beta1 = -2.0 * m_real[m];
                var beta2 = Sqr(m_real[m]) + Sqr(m_imag[m]);

                newA[1] = m_aCoeff[1] + alpha1 * m_aCoeff[0];
                newB[1] = m_bCoeff[1] + beta1 * m_bCoeff[0];

                for (var i = 2; i <= n; i++)
                {
                    newA[i] = m_aCoeff[i] + alpha1 * m_aCoeff[i - 1] + alpha2 * m_aCoeff[i - 2];
                    newB[i] = m_bCoeff[i] + beta1 * m_bCoeff[i - 1] + beta2 * m_bCoeff[i - 2];
                }

                for (var i = 1; i <= n; i++)
                {
                    m_aCoeff[i] = newA[i];
                    m_bCoeff[i] = newB[i];
                }
            }

            // Ensure the filter is normalized
            FilterGain(1000);
        }

        /// <summary>
        /// Reset the history buffers
        /// </summary>
        public void Reset()
        {
            if (m_inHistory != null) Array.Clear(m_inHistory, 0, m_inHistory.Length);

            if (m_outHistory != null) Array.Clear(m_outHistory, 0, m_outHistory.Length);

            m_histIdx = 0;
        }

        /// <summary>
        /// Apply the filter to the Buffer
        /// </summary>
        public void FilterBuffer(ReadOnlySpan<float> inBuffer, int inBufferOffset, Span<float> outBuffer,
            int outBufferOffset, int size)
        {
            const double kDenormal = 0.000000000000001;
            var denormal = m_invertDenormal ? -kDenormal : kDenormal;
            m_invertDenormal = !m_invertDenormal;

            for (var sampleIdx = 0; sampleIdx < size; sampleIdx++)
            {
                m_inHistory[m_histIdx] = inBuffer[inBufferOffset + sampleIdx] + denormal;

                var sum = m_aCoeff.Select((t, idx) => t * m_inHistory[(m_histIdx - idx) & kHistMask]).Sum();

                for (var idx = 1; idx < m_bCoeff.Length; idx++)
                    sum -= m_bCoeff[idx] * m_outHistory[(m_histIdx - idx) & kHistMask];

                m_outHistory[m_histIdx] = sum;
                m_histIdx = (m_histIdx + 1) & kHistMask;
                outBuffer[outBufferOffset + sampleIdx] = (float) sum;
            }
        }

        /// <summary>
        /// Get the gain at the specified number of frequency points
        /// </summary>
        /// <param name="freqPoints"></param>
        /// <returns></returns>
        public float[] FilterGain(int freqPoints)
        {
            // Filter gain at uniform frequency intervals
            var g = new float[freqPoints];

            var gMax = -100f;
            var sc = 10 / (float) Math.Log(10);
            var t = Math.PI / (freqPoints - 1);

            for (var i = 0; i < freqPoints; i++)
            {
                var theta = i * t;

                if (i == 0) theta = Math.PI * 0.0001;

                if (i == freqPoints - 1) theta = Math.PI * 0.9999;

                double sac = 0, sas = 0, sbc = 0, sbs = 0;

                for (var k = 0; k <= m_order; k++)
                {
                    var c = Math.Cos(k * theta);
                    var s = Math.Sin(k * theta);
                    sac += c * m_aCoeff[k];
                    sas += s * m_aCoeff[k];
                    sbc += c * m_bCoeff[k];
                    sbs += s * m_bCoeff[k];
                }

                g[i] = sc * (float) Math.Log((Sqr(sac) + Sqr(sas)) / (Sqr(sbc) + Sqr(sbs)));
                gMax = Math.Max(gMax, g[i]);
            }

            // Normalize to 0 dB maximum gain
            for (var i = 0; i < freqPoints; i++) g[i] -= gMax;

            // Normalize numerator (a) coefficients
            var normFactor = (float) Math.Pow(10.0f, -0.05f * gMax);

            for (var i = 0; i <= m_order; i++) m_aCoeff[i] *= normFactor;

            return g;
        }
    }
}
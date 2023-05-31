using System;

namespace YARG.Audio {
	public static class FFT {
		private readonly struct Complex {
			public readonly double Real;
			public readonly double Imaginary;

			public Complex(double real, double imaginary) {
				Real = real;
				Imaginary = imaginary;
			}

			public double Magnitude() {
				return Math.Sqrt(Real * Real + Imaginary * Imaginary);
			}

			public static Complex operator +(Complex a, Complex b) {
				return new Complex(
					a.Real + b.Real,
					a.Imaginary + b.Imaginary
				);
			}

			public static Complex operator -(Complex a, Complex b) {
				return new Complex(
					a.Real - b.Real,
					a.Imaginary - b.Imaginary
				);
			}

			public static Complex operator *(Complex a, Complex b) {
				return new Complex(
					a.Real * b.Real - a.Imaginary * b.Imaginary,
					a.Real * b.Imaginary + a.Imaginary * b.Real
				);
			}
		}

		private static ReadOnlySpan<Complex> DoInternal(ReadOnlySpan<Complex> samples, bool inverse = false) {
			if (samples.Length <= 1) {
				return samples;
			}

			int halfLen = samples.Length / 2;

			// TODO: That's a lot of allocation
			var evenIn = new Complex[halfLen];
			var oddIn = new Complex[halfLen];

			for (int i = 0; i < halfLen; i++) {
				evenIn[i] = samples[2 * i];
				oddIn[i] = samples[2 * i + 1];
			}

			var even = DoInternal(evenIn, inverse);
			var odd = DoInternal(oddIn, inverse);

			double angle = 2 * Math.PI / samples.Length * (inverse ? -1 : 1);
			var w = new Complex(1.0, 0.0);
			var twiddle = new Complex(Math.Cos(angle), Math.Sin(angle));

			var spectrum = new Complex[samples.Length];
			for (int i = 0; i < halfLen; i++) {
				var t = w * odd[i];
				spectrum[i] = even[i] + t;
				spectrum[i + halfLen] = even[i] - t;
				w *= twiddle;
			}

			return spectrum;
		}

		public static void Do(ReadOnlySpan<float> samples, Span<float> output, bool inverse = false) {
			// Convert to complex
			Span<Complex> complex = stackalloc Complex[samples.Length];
			for (int i = 0; i < samples.Length; i++) {
				complex[i] = new Complex(samples[i], 0f);
			}

			// Run
			var complexOut = DoInternal(complex, inverse);

			// Convert into magnitude form
			for (int i = 0; i < samples.Length; i++) {
				output[i] = (float) complexOut[i].Magnitude();
			}
		}
	}
}
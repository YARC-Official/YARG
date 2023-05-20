using System;

namespace YARG {
	public interface ISampleChannel : IDisposable {

		public SfxSample Sample { get; }

		public int Load();

		public void Play();

	}
}
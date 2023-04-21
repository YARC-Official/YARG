using System;

namespace YARG {
	public interface IStemChannel : IDisposable {

		public SongStem Stem { get; }
		public double LengthD { get; }
		public float LengthF => (float)LengthD;

		public int Load(bool isSpeedUp, float speed);
		
		public void SetVolume(double volume);
		
		public void SetReverb(bool reverb);

		public double GetPosition();
		
		public double GetLengthInSeconds();

	}
}

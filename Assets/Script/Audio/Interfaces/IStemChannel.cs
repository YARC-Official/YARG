using System;
using Cysharp.Threading.Tasks;

namespace YARG {
	public interface IStemChannel : IDisposable {

		public SongStem Stem { get; }
		public double LengthD { get; }
		public float LengthF => (float)LengthD;
		
		public double Volume { get; }

		public int Load(bool isSpeedUp, float speed);

		public void FadeIn(float maxVolume);
		public UniTask FadeOut();
		
		public void SetVolume(double newVolume);
		
		public void SetReverb(bool reverb);

		public double GetPosition();
		
		public double GetLengthInSeconds();

	}
}

using System;
using Cysharp.Threading.Tasks;

namespace YARG.Audio {
	public interface IStemChannel : IDisposable {

		public SongStem Stem { get; }
		public double LengthD { get; }
		public float LengthF => (float)LengthD;

		public double Volume { get; }

		public event Action ChannelEnd;

		public int Load(float speed);

		public void FadeIn(float maxVolume);
		public UniTask FadeOut();

		public void SetVolume(double newVolume);

		public void SetReverb(bool reverb);

		public double GetPosition();
		public void SetPosition(double position);

		public double GetLengthInSeconds();

	}
}

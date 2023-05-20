using System;
using Cysharp.Threading.Tasks;
using YARG.Song;

namespace YARG {
	public interface IPreviewContext : IDisposable {

		public double PreviewStartTime { get; }
		public double PreviewEndTime { get; }

		public UniTask StartLooping();

		public UniTask PlayPreview(SongEntry song);
		public void StopPreview();

	}
}
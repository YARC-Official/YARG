using System;
using ManagedBass;

namespace YARG.Audio.BASS {
	public class BassSampleChannel : ISampleChannel {

		public SfxSample Sample { get; }

		private readonly IAudioManager _manager;
		private readonly string _path;
		private readonly int _playbackCount;

		private int _sfxHandle;

		private bool _disposed;

		public BassSampleChannel(IAudioManager manager, string path, int playbackCount, SfxSample sample) {
			_manager = manager;
			_path = path;
			_playbackCount = playbackCount;

			Sample = sample;
		}

		~BassSampleChannel() {
			Dispose(false);
		}

		public int Load() {
			if (_sfxHandle != 0) {
				return 0;
			}

			int handle = Bass.SampleLoad(_path, 0, 0, _playbackCount, BassFlags.Decode);
			if (handle == 0) {
				return (int)Bass.LastError;
			}

			_sfxHandle = handle;
			return 0;
		}

		public void Play() {
			if (_sfxHandle == 0)
				return;

			int channel = Bass.SampleGetChannel(_sfxHandle);

			double volume = _manager.GetVolumeSetting(SongStem.Sfx) * AudioHelpers.SfxVolume[(int) Sample];
			Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, volume);

			Bass.ChannelPlay(channel);
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			if (!_disposed) {
				// Free managed resources here
				if (disposing) {

				}

				// Free unmanaged resources here
				if (_sfxHandle != 0) {
					Bass.SampleFree(_sfxHandle);
				}

				_disposed = true;
			}
		}
	}
}

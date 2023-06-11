using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace YARG.Audio.BASS {
	public class BassMoggStemChannel : IStemChannel {

		public SongStem Stem { get; }
		public double LengthD { get; private set; }
		public double Volume { get; private set; }

		public event Action ChannelEnd {
			add {
				if (_leadChannel is null) {
					throw new InvalidOperationException("No song is currently loaded!");
				}

				_leadChannel.ChannelEnd += value;
			}
			remove {
				if (_leadChannel is null) {
					throw new InvalidOperationException("No song is currently loaded!");
				}

				_leadChannel.ChannelEnd -= value;
			}
		}

		private readonly IAudioManager _manager;

		private IStemChannel _leadChannel;
		private List<IStemChannel> _channels = new();
		public IReadOnlyList<IStemChannel> Channels => _channels;

		private List<float[]> _matrixes;
		public IReadOnlyList<float[]> Matrixes => _matrixes;

		private bool _isLoaded;
		private bool _disposed;

		public BassMoggStemChannel(IAudioManager manager, SongStem stem, int[] splitStreams, List<float[]> matrixes) {
			_manager = manager;
			Stem = stem;

			foreach (int stream in splitStreams) {
				_channels.Add(new BassStemChannel(_manager, Stem, stream));
			}

			_matrixes = matrixes;

			Volume = 1;
		}

		~BassMoggStemChannel() {
			Dispose(false);
		}

		public int Load(float speed) {
			if (_disposed) {
				return -1;
			}
			if (_isLoaded) {
				return 0;
			}

			foreach (var channel in _channels) {
				channel.Load(speed);

				// Get longest channel as part of this stem
				double length = channel.LengthD;
				if (length > LengthD) {
					_leadChannel = channel;
					LengthD = length;
				}
			}

			_isLoaded = true;

			return 0;
		}

		public void FadeIn(float maxVolume) {
			foreach (var channel in _channels) {
				channel.FadeIn(maxVolume);
			}
		}

		public UniTask FadeOut() {
			var fadeOuts = new List<UniTask>();
			foreach (var channel in _channels) {
				fadeOuts.Add(channel.FadeOut());
			}

			return UniTask.WhenAll(fadeOuts);
		}

		public void SetVolume(double newVolume) {
			if (!_isLoaded) {
				return;
			}

			Volume = newVolume;

			foreach (var channel in _channels) {
				channel.SetVolume(newVolume);
			}
		}

		public void SetReverb(bool reverb) {
			foreach (var channel in _channels) {
				channel.SetReverb(reverb);
			}
		}

		public double GetPosition() {
			return _leadChannel.GetPosition();
		}

		public void SetPosition(double position) {
			foreach (var channel in _channels) {
				channel.SetPosition(position);
			}
		}

		public double GetLengthInSeconds() {
			return _leadChannel.GetLengthInSeconds();
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			if (!_disposed) {
				// Free managed resources here
				if (disposing) {
					foreach (var channel in _channels) {
						channel.Dispose();
					}
					_channels = null;
				}

				_disposed = true;
			}
		}
	}
}


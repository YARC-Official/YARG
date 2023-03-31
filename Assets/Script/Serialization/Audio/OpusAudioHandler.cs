using System.IO;
using Concentus.Oggfile;
using Concentus.Structs;
using UnityEngine;

namespace YARG.Serialization.Audio {
	/// <summary>
	/// Support for OPUS via Concentus.
	/// </summary>
	public sealed class OpusAudioHandler : AudioHandler {
		private FileStream fileStream;
		private OpusOggReadStream readStream;

		public OpusAudioHandler(string path) : base(path) {

		}

		public override AsyncOperation LoadAudioClip() {
			var fileStream = new FileStream(path, FileMode.Open);

			var decoder = new OpusDecoder(48000, 2);
			readStream = new OpusOggReadStream(decoder, fileStream);

			// while (readStream.HasNextPacket) {
			// 	short[] packet = readStream.DecodeNextPacket();
			// 	if (packet == null) {
			// 		continue;
			// 	}

			// 	for (int i = 0; i < packet.Length; i++) {
			// 		var bytes = BitConverter.GetBytes(packet[i]);
			// 	}
			// }

			return null;
		}

		public override AudioClip GetAudioClipResult() {
			var audio = AudioClip.Create("Opus Stream", (int) readStream.GranuleCount,
				2, 48000, false);

			return null;
		}

		public override void Finish() {
			fileStream.Dispose();
		}
	}
}
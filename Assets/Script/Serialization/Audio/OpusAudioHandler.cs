using System.Collections.Generic;
using System.IO;
using Concentus.Oggfile;
using Concentus.Structs;
using UnityEngine;

namespace YARG.Serialization.Audio {
	/// <summary>
	/// Support for OPUS via Concentus.
	/// </summary>
	public sealed class OpusAudioHandler : AudioHandler {
		private AudioClip clip;

		public OpusAudioHandler(string path) : base(path) {

		}

		public override AsyncOperation LoadAudioClip() {
			using var fileStream = new FileStream(path, FileMode.Open);

			var decoder = new OpusDecoder(48000, 2);
			var readStream = new OpusOggReadStream(decoder, fileStream);

			var data = new List<float>();

			// Decode the .opus file
			while (readStream.HasNextPacket) {
				short[] packet = readStream.DecodeNextPacket();
				if (packet == null) {
					continue;
				}

				for (int i = 0; i < packet.Length; i++) {
					// Convert the short to a float
					const float SCALE = 1f / short.MaxValue;
					data.Add(packet[i] * SCALE);
				}
			}

			// Create audio clip and set the data
			clip = AudioClip.Create("Opus Clip", data.Count, 2, 48000, false);
			clip.SetData(data.ToArray(), 0);

			return null;
		}

		public override AudioClip GetAudioClipResult() {
			return clip;
		}

		public override void Finish() {
			clip.UnloadAudioData();
		}
	}
}
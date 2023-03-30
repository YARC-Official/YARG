using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace YARG.Serialization.Audio {
	/// <summary>
	/// Built-in Unity support for OGG, WAV, MP3, and AIFF.
	/// </summary>
	public class WWWAudioHandler : AudioHandler {
		private UnityWebRequest uwr;

		public WWWAudioHandler(string path) : base(path) {

		}

		public override AsyncOperation LoadAudioClip() {
			AudioType type = AudioType.UNKNOWN;
			string extension = Path.GetExtension(path).ToLowerInvariant();
			if (extension == ".ogg") {
				type = AudioType.OGGVORBIS;
			} else if (extension == ".wav") {
				type = AudioType.WAV;
			} else if (extension == ".mp3") {
				type = AudioType.MPEG;
			} else if (extension == ".aiff") {
				type = AudioType.AIFF;
			}

			uwr = UnityWebRequestMultimedia.GetAudioClip(path, type);
			((DownloadHandlerAudioClip) uwr.downloadHandler).streamAudio = true;
			return uwr.SendWebRequest();
		}

		public override AudioClip GetAudioClipResult() {
			return DownloadHandlerAudioClip.GetContent(uwr);
		}
	}
}
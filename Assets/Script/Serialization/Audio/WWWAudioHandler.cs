using System.Collections.Generic;
using UnityEngine;

namespace YARG.Serialization.Audio {
	public class WWWAudioHandler : AudioHandler {
		UnityWebRequest uwr;

		public WWWAudioHandler() {

			yield return uwr.SendWebRequest();
			var clip = DownloadHandlerAudioClip.GetContent(uwr);
		}

		public IEnumerator LoadAudioClip(string path) {
			uwr = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.OGGVORBIS);
			((DownloadHandlerAudioClip) uwr.downloadHandler).streamAudio = true;
		}

		public AudioClip GetAudioClipResult() {

		}
	}
}
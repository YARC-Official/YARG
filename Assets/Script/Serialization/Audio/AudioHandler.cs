using System;
using System.Collections.Generic;

namespace YARG.Serialization.Audio {
	/// <summary>
	/// One time use audio loader. This is used to load audio clips from files.
	/// </summary>
	public abstract class AudioHandler {
		private static Dictionary<string, Type> audioHandlers = new();

		static AudioHandler() {
			//audioHandlers.Add("ogg", new SoundAudioHandler());
		}

		/// <summary>
		/// Called when the audio clip should be loaded.
		/// </summary>
		public IEnumerator LoadAudioClip(string path);
		/// <summary>
		/// Returns the resulting audio clip. No file loading should be done here.
		/// </summary>
		public AudioClip GetAudioClipResult();
	}
}
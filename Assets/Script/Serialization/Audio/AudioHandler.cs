using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace YARG.Serialization.Audio {
	/// <summary>
	/// One time use audio loader. This is used to load audio clips from files.
	/// </summary>
	public abstract class AudioHandler {
		private static Dictionary<string, Type> audioHandlers = new();

		static AudioHandler() {
			audioHandlers.Add(".ogg", typeof(WWWAudioHandler));
			audioHandlers.Add(".wav", typeof(WWWAudioHandler));
			audioHandlers.Add(".mp3", typeof(WWWAudioHandler));
			audioHandlers.Add(".aiff", typeof(WWWAudioHandler));
		}

		protected string path;

		protected AudioHandler(string path) {
			this.path = path;
		}

		/// <summary>
		/// Called when the audio clip should be loaded.
		/// </summary>
		public abstract AsyncOperation LoadAudioClip();
		/// <summary>
		/// Returns the resulting audio clip. No file loading should be done here.
		/// </summary>
		public abstract AudioClip GetAudioClipResult();

		public static AudioHandler CreateAudioHandler(string path) {
			string extension = Path.GetExtension(path).ToLowerInvariant();

			if (audioHandlers.TryGetValue(extension, out var type)) {
				return (AudioHandler) Activator.CreateInstance(type, path);
			}

			Debug.LogError("No audio handler for extension: " + extension);
			return null;
		}

		public static List<string> GetAllSupportedAudioFiles(string directory) {
			List<string> files = new();
			foreach (var file in Directory.GetFiles(directory)) {
				string extension = Path.GetExtension(file).ToLowerInvariant();
				if (audioHandlers.ContainsKey(extension)) {
					files.Add(file);
				}
			}

			return files;
		}
	}
}
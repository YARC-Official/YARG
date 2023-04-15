using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace YARG {
	public class AudioManager : MonoBehaviour {
		public static AudioManager Instance {
			get;
			private set;
		} = null;

		/// <summary>
		/// Unity inspector class ('cause we can't use dictionaries for some reason).
		/// </summary>
		[Serializable]
		public class KVP {
			public string id;
			public AudioMixerGroup mixer;
		}

		[SerializeField]
		private AudioMixer audioMixer = null;
		[SerializeField]
		private List<KVP> audioChannelsInspector = new();

		private Dictionary<string, AudioMixerGroup> audioChannels = new();

		private void Awake() {
			Instance = this;

			// Convert the inspector list to a dictionary
			foreach (var kvp in audioChannelsInspector) {
				audioChannels.Add(kvp.id, kvp.mixer);
			}
		}

		public void SetVolume(string name, float volume) {
			audioMixer.SetFloat($"{name}_volume", VolumeFromLinear(volume));
		}

		public AudioMixerGroup GetAudioMixerGroup(string name) {
			if (audioChannels.TryGetValue(name, out var mixer)) {
				return mixer;
			}

			return null;
		}

		/// <param name="v">A linear volume between 0 and 1.</param>
		/// <returns>
		/// The linear volume converted to decibels.
		/// </returns>
		private static float VolumeFromLinear(float v) {
			return Mathf.Log10(Mathf.Min(v + float.Epsilon, 1f)) * 20f;
		}
	}
}
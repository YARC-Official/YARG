using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering.Universal;
using YARG.Input;
using YARG.UI;

namespace YARG.PlayMode {
	public class MicPlayer : MonoBehaviour {
		public static MicPlayer Instance {
			get; private set;
		}

		[SerializeField]
		private Transform needle;
		[SerializeField]
		private Camera trackCamera;

		[SerializeField]
		private AudioMixerGroup silentMixerGroup;

		private List<MicInputStrategy> micInputs = new();
		public Dictionary<MicInputStrategy, AudioSource> dummyAudioSources = new();

		private void Start() {
			Instance = this;

			// Start mics
			bool hasMic = false;
			foreach (var player in PlayerManager.players) {
				if (player.inputStrategy is MicInputStrategy micStrategy) {
					if (micStrategy.microphoneIndex == -1) {
						continue;
					}

					hasMic = true;

					// Add to inputs
					micInputs.Add(micStrategy);

					// Add child dummy audio source (for mic input reading)
					var go = new GameObject();
					go.transform.parent = transform;
					var audio = go.AddComponent<AudioSource>();
					dummyAudioSources.Add(micStrategy, audio);
					audio.outputAudioMixerGroup = silentMixerGroup;
					audio.loop = true;

					// Start the mic!
					var micName = Microphone.devices[micStrategy.microphoneIndex];
					audio.clip = Microphone.Start(micName, true, 1, AudioSettings.outputSampleRate);

					// Wait for the mic to start, then start the audio
					while (Microphone.GetPosition(micName) <= 0) {
						// This loop is weird, but it works.
					}
					audio.Play();
				}
			}

			// Destroy if no mic is connected
			if (!hasMic) {
				Destroy(gameObject);
				return;
			}

			// Set up render texture
			var descriptor = new RenderTextureDescriptor(
				Screen.width, Screen.height,
				RenderTextureFormat.ARGBHalf
			);
			descriptor.mipCount = 0;
			var renderTexture = new RenderTexture(descriptor);
			trackCamera.targetTexture = renderTexture;

			// Set up camera
			var info = trackCamera.GetComponent<UniversalAdditionalCameraData>();
			if (GameManager.Instance.LowQualityMode) {
				info.antialiasing = AntialiasingMode.None;
			} else {
				info.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
				info.antialiasingQuality = AntialiasingQuality.Low;
			}

			// Set render texture on UI
			GameUI.Instance.SetVocalTrackImage(renderTexture);
		}

		private void OnDestroy() {
			// Release render texture
			trackCamera.targetTexture.Release();
		}

		private void Update() {
			foreach (var inputStrategy in micInputs) {
				inputStrategy.UpdatePlayerMode();
			}

			needle.gameObject.SetActive(micInputs[0].VoiceDetected);

			float z = -0.353f +
				(micInputs[0].VoiceNote / 12f * 0.42f) +
				(micInputs[0].VoiceOctave - 3) * 0.42f;
			z = Mathf.Clamp(z, -0.45f, 0.93f);

			needle.transform.localPosition = needle.transform.localPosition.WithZ(z);
		}
	}
}

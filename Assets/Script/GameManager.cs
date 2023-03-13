using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using YARG.Server;

namespace YARG {
	public enum SceneIndex {
		PERSISTANT,
		MENU,
		PLAY,
		SERVER_HOST,
		CALIBRATION
	}

	public class GameManager : MonoBehaviour {
		public static GameManager Instance {
			get;
			private set;
		}

		public static Client client;

		[field: SerializeField]
		public Camera MainCamera {
			get;
			private set;
		}

		[SerializeField]
		private AudioMixerGroup vocalGroup;

		private bool _lowQualityMode = false;
		public bool LowQualityMode {
			get => _lowQualityMode;
			set {
				_lowQualityMode = value;

				QualitySettings.SetQualityLevel(_lowQualityMode ? 0 : 1, true);
			}
		}

		private bool _karaokeMode = false;
		public bool KaraokeMode {
			get => _karaokeMode;
			set {
				_karaokeMode = value;

				vocalGroup.audioMixer.SetFloat("vocalVolume", _karaokeMode ? 5f : -10f);
			}
		}

		public bool showHitWindow = false;
		public bool useAudioTime = false;

		private SceneIndex currentScene = SceneIndex.PERSISTANT;

		private void Start() {
			Instance = this;

			// Unlimited FPS
			QualitySettings.vSyncCount = 0;
			Application.targetFrameRate = 400;

			// High polling rate
			InputSystem.pollingFrequency = 500f;

			LoadScene(SceneIndex.MENU);
		}

		private void LoadSceneAdditive(SceneIndex scene) {
			var asyncOp = SceneManager.LoadSceneAsync((int) scene, LoadSceneMode.Additive);
			asyncOp.completed += _ => {
				// When complete, set the newly loaded scene to the active one
				currentScene = scene;
				SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex((int) scene));
			};
		}

		public void LoadScene(SceneIndex scene) {
			// Unload the current scene and load in the new one, or just load in the new one
			if (currentScene != SceneIndex.PERSISTANT) {
				var asyncOp = SceneManager.UnloadSceneAsync((int) currentScene);
				asyncOp.completed += _ => LoadSceneAdditive(scene);
			} else {
				LoadSceneAdditive(scene);
			}
		}
	}
}
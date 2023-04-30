using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using YARG.Settings;

namespace YARG {
	public enum SceneIndex {
		PERSISTANT,
		MENU,
		PLAY,
		CALIBRATION
	}

	public class GameManager : MonoBehaviour {
		public static GameManager Instance { get; private set; }

		public delegate void UpdateAction();
		public static event UpdateAction OnUpdate;

		/// <summary>
		/// "Application.persistentDataPath" is main thread only. Why? I don't know.
		/// </summary>
		public static string PersistentDataPath { get; private set; }

		public static IAudioManager AudioManager { get; private set; }

		[field: SerializeField]
		public SettingsMenu SettingsMenu { get; private set; }

		[SerializeField]
		private AudioMixerGroup vocalGroup;

		public SceneIndex CurrentScene { get; private set; } = SceneIndex.PERSISTANT;

		private void Awake() {
			Instance = this;

			Debug.Log($"YARG {Constants.VERSION_TAG}");

			AudioManager = gameObject.AddComponent<BassAudioManager>();
			AudioManager.Initialize();
		}

		private void Start() {
			// this is to handle a strange edge case in path naming in windows.
			// modern windows can handle / or \ in path names with seemingly one exception, if there is a space in the user name then try forward slash appdata, it will break at the first space so:
			// c:\users\joe blow\appdata <- okay!
			// c:/users/joe blow\appdata <- okay!
			// c:/users/joe blow/appdata <- "Please choose an app to open joe"
			// so let's just set them all to \ on windows to be sure.
			// For linux Path.DirectorySeparatorChar should return /, and this should work fine, but this should be double checked once those builds are being worked on
			PersistentDataPath = Application.persistentDataPath.Replace("/", Path.DirectorySeparatorChar.ToString());
			SettingsManager.LoadSettings();

			// High polling rate
			InputSystem.pollingFrequency = 500f;

			LoadScene(SceneIndex.MENU);
		}

		private void OnDestroy() {
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.Disable();
			}
		}

		private void Update() {
			OnUpdate?.Invoke();
		}

#if UNITY_EDITOR
		private void OnGUI() {
			// FPS and Memory
			GUI.skin.label.fontSize = 20;
			GUI.color = Color.green;
			GUI.Label(new Rect(10, 20, 500, 40), $"FPS: {1f / Time.unscaledDeltaTime:0.0}");
			GUI.Label(new Rect(10, 40, 500, 40), $"Memory: {Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024} MB");
		}
#endif

		private void LoadSceneAdditive(SceneIndex scene) {
			var asyncOp = SceneManager.LoadSceneAsync((int) scene, LoadSceneMode.Additive);
			asyncOp.completed += _ => {
				// When complete, set the newly loaded scene to the active one
				CurrentScene = scene;
				SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex((int) scene));
			};
		}

		public void LoadScene(SceneIndex scene) {
			// Unload the current scene and load in the new one, or just load in the new one
			if (CurrentScene != SceneIndex.PERSISTANT) {
				// Unload the current scene
				var asyncOp = SceneManager.UnloadSceneAsync((int) CurrentScene);

				// The load the new scene
				asyncOp.completed += _ => LoadSceneAdditive(scene);
			} else {
				LoadSceneAdditive(scene);
			}
		}
	}
}

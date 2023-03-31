using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
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

		public delegate void UpdateAction();
		public static event UpdateAction OnUpdate;

		[SerializeField]
		private AudioMixerGroup vocalGroup;

		private SceneIndex currentScene = SceneIndex.PERSISTANT;

		private void Start() {
			Instance = this;

			// Unlimited FPS (if vsync is off)
			Application.targetFrameRate = 400;

			// High polling rate
			InputSystem.pollingFrequency = 500f;

			LoadScene(SceneIndex.MENU);
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
				currentScene = scene;
				SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex((int) scene));
			};
		}

		public void LoadScene(SceneIndex scene) {
			// Unload the current scene and load in the new one, or just load in the new one
			if (currentScene != SceneIndex.PERSISTANT) {
				// Unload the current scene
				var asyncOp = SceneManager.UnloadSceneAsync((int) currentScene);

				// The load the new scene
				asyncOp.completed += _ => LoadSceneAdditive(scene);
			} else {
				LoadSceneAdditive(scene);
			}
		}
	}
}
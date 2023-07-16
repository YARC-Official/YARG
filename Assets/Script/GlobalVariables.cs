using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using YARG.Audio;
using YARG.Audio.BASS;
using YARG.Player;
using YARG.Player.Input;
using YARG.Replays;
using YARG.Settings;
using YARG.Song;
using YARG.Util;

namespace YARG
{
    public enum SceneIndex
    {
        Persistent,
        Menu,
        Gameplay,
        Calibration
    }

    public class GlobalVariables : MonoBehaviour
    {
        public static GlobalVariables Instance { get; private set; }

        public List<YargPlayer> Players { get; private set; }

        public static IAudioManager AudioManager { get; private set; }

        [field: SerializeField]
        public SettingsMenu SettingsMenu { get; private set; }

        public SceneIndex CurrentScene { get; private set; } = SceneIndex.Persistent;

        public SongEntry CurrentSong;

        public float songSpeed = 1f;
        public bool  isReplay;

#if UNITY_EDITOR
        public Util.TestPlayInfo TestPlayInfo { get; private set; }
#endif

        private void Awake()
        {
            Debug.Log($"YARG {Constants.VERSION_TAG}");
            Instance = this;

            ConsoleRedirect.Redirect();

            PathHelper.Init();
            ReplayContainer.Init();

            int profileCount = PlayerContainer.LoadProfiles();
            Debug.Log($"Loaded {profileCount} profiles");

            int savedCount = PlayerContainer.SaveProfiles();
            Debug.Log($"Saved {savedCount} profiles");

            AudioManager = gameObject.AddComponent<BassAudioManager>();
            AudioManager.Initialize();

            Players = new List<YargPlayer>();

            Shader.SetGlobalFloat("_IsFading", 1f);

            StageKitHapticsManager.Initialize();

#if UNITY_EDITOR
            TestPlayInfo =
                UnityEditor.AssetDatabase.LoadAssetAtPath<Util.TestPlayInfo>("Assets/Settings/TestPlayInfo.asset");
#endif
        }

        private void Start()
        {
            SettingsManager.LoadSettings();

            // High polling rate
            InputSystem.pollingFrequency = 500f;

            LoadScene(SceneIndex.Menu);
        }

        private void LoadSceneAdditive(SceneIndex scene)
        {
            var asyncOp = SceneManager.LoadSceneAsync((int) scene, LoadSceneMode.Additive);
            CurrentScene = scene;
            asyncOp.completed += _ =>
            {
                // When complete, set the newly loaded scene to the active one
                SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex((int) scene));
            };
        }

        public void LoadScene(SceneIndex scene)
        {
            // Unload the current scene and load in the new one, or just load in the new one
            if (CurrentScene != SceneIndex.Persistent)
            {
                // Unload the current scene
                var asyncOp = SceneManager.UnloadSceneAsync((int) CurrentScene);

                // The load the new scene
                asyncOp.completed += _ => LoadSceneAdditive(scene);
            }
            else
            {
                LoadSceneAdditive(scene);
            }
        }
    }
}
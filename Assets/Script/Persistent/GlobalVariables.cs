using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Audio;
using YARG.Audio.BASS;
using YARG.Core;
using YARG.Helpers;
using YARG.Input;
using YARG.Menu.Settings;
using YARG.Player;
using YARG.Replays;
using YARG.Settings;
using YARG.Song;

namespace YARG
{
    public enum SceneIndex
    {
        Persistent,
        Menu,
        Gameplay,
        Calibration
    }

    [DefaultExecutionOrder(-5000)]
    public class GlobalVariables : MonoSingleton<GlobalVariables>
    {
        public static readonly YargVersion CurrentVersion = YargVersion.Parse("v0.11.0");

        public List<YargPlayer> Players { get; private set; }

        public static IAudioManager AudioManager { get; private set; }

        [field: SerializeField]
        public SettingsMenu SettingsMenu { get; private set; }

        public SceneIndex CurrentScene { get; private set; } = SceneIndex.Persistent;

        public SongEntry   CurrentSong;
        public ReplayEntry CurrentReplay;

        public float SongSpeed = 1f;
        public bool  IsReplay;
        public bool  IsPractice;

        protected override void SingletonAwake()
        {
            Debug.Log($"YARG {CurrentVersion}");

            YargTrace.AddListener(new YargUnityTraceListener());

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
        }

        private void Start()
        {
            SettingsManager.LoadSettings();
            InputManager.Initialize();

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
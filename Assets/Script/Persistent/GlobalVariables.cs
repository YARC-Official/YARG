using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Audio;
using YARG.Audio.BASS;
using YARG.Core;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Input;
using YARG.Integration;
using YARG.Menu.ScoreScreen;
using YARG.Player;
using YARG.Playlists;
using YARG.Replays;
using YARG.Scores;
using YARG.Settings;
using YARG.Settings.Customization;
using YARG.Song;

namespace YARG
{
    public enum SceneIndex
    {
        Persistent,
        Menu,
        Gameplay,
        Calibration,
        Score
    }

    [DefaultExecutionOrder(-5000)]
    public class GlobalVariables : MonoSingleton<GlobalVariables>
    {
        public const string CURRENT_VERSION = "v0.12.1";

        private const string OFFLINE_ARG = "-offline";

        public List<YargPlayer> Players { get; private set; }

        public static IReadOnlyList<string> CommandLineArguments { get; private set; }
        public static bool OfflineMode { get; private set; }

        public static IAudioManager AudioManager { get; private set; }

        public SceneIndex CurrentScene { get; private set; } = SceneIndex.Persistent;

        [HideInInspector]
        public SongEntry CurrentSong;
        public ReplayEntry  CurrentReplay;

        public ScoreScreenStats ScoreScreenStats;

        [Space]
        public float SongSpeed = 1f;

        [HideInInspector]
        public bool IsReplay;
        [HideInInspector]
        public bool IsPractice;

        protected override void SingletonAwake()
        {
            Debug.Log($"YARG {CURRENT_VERSION}");
            YargTrace.AddListener(new YargUnityTraceListener());

            // Get command line args
            // The first element is always the file name, however check just in case
            var args = Environment.GetCommandLineArgs();
            if (args.Length >= 1)
            {
                CommandLineArguments = args[1..].ToList();
            }
            else
            {
                CommandLineArguments = new List<string>();
            }

            // Check for offline mode
            OfflineMode = CommandLineArguments.Contains(OFFLINE_ARG);
            if (OfflineMode)
            {
                Debug.Log("Playing in offline mode");
            }

            // Initialize important classes
            PathHelper.Init();
            ReplayContainer.Init();
            ScoreContainer.Init();
            PlaylistContainer.Initialize();
            CustomContentManager.Initialize();

            int profileCount = PlayerContainer.LoadProfiles();
            Debug.Log($"Loaded {profileCount} profiles");

            int savedCount = PlayerContainer.SaveProfiles();
            Debug.Log($"Saved {savedCount} profiles");

            AudioManager = gameObject.AddComponent<BassAudioManager>();
            AudioManager.Initialize();

            Players = new List<YargPlayer>();

            // Set alpha fading (on the tracks) to on
            // (this is mostly for the editor, but just in case)
            Shader.SetGlobalFloat("_IsFading", 1f);
        }

        private void Start()
        {
            SettingsManager.LoadSettings();
            InputManager.Initialize();

            LoadScene(SceneIndex.Menu);
        }

        protected override void SingletonDestroy()
        {
            SettingsManager.SaveSettings();
            PlayerContainer.SaveProfiles();
            PlaylistContainer.SaveAll();
            CustomContentManager.SaveAll();

            ReplayContainer.Destroy();
            ScoreContainer.Destroy();
            InputManager.Destroy();
            PlayerContainer.Destroy();

#if UNITY_EDITOR
            // Set alpha fading (on the tracks) to off
            Shader.SetGlobalFloat("_IsFading", 0f);
#endif
        }

        private void LoadSceneAdditive(SceneIndex scene)
        {
            var asyncOp = SceneManager.LoadSceneAsync((int) scene, LoadSceneMode.Additive);

            CurrentScene = scene;

            GameStateFetcher.SetSceneIndex(scene);

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
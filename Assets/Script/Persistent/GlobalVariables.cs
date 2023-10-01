using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Audio;
using YARG.Audio.BASS;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Song;
using YARG.Core.Song.Cache;
using YARG.Core.Utility;
using YARG.Helpers;
using YARG.Input;
using YARG.Integration;
using YARG.Menu.ScoreScreen;
using YARG.Menu.Settings;
using YARG.Player;
using YARG.Replays;
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
        public static readonly YargVersion CurrentVersion = YargVersion.Parse("v0.12.0-a5");

        public List<YargPlayer> Players { get; private set; }

        public static IAudioManager AudioManager { get; private set; }

        [field: SerializeField]
        public SettingsMenu SettingsMenu { get; private set; }

        public SceneIndex CurrentScene { get; private set; } = SceneIndex.Persistent;

        public SongContainer SongContainer { get; private set; }
        public SongSorting   SortedSongs   { get; private set; }

        public SongMetadata CurrentSong;
        public ReplayEntry  CurrentReplay;

        public ScoreScreenStats ScoreScreenStats;

        [Space]
        public float SongSpeed = 1f;
        public bool IsReplay;
        public bool IsPractice;

        protected override void SingletonAwake()
        {
            Debug.Log($"YARG {CurrentVersion}");

            YargTrace.AddListener(new YargUnityTraceListener());

            PathHelper.Init();
            ReplayContainer.Init();
            CustomContentManager.Init();

            int profileCount = PlayerContainer.LoadProfiles();
            Debug.Log($"Loaded {profileCount} profiles");

            int savedCount = PlayerContainer.SaveProfiles();
            Debug.Log($"Saved {savedCount} profiles");

            AudioManager = gameObject.AddComponent<BassAudioManager>();
            AudioManager.Initialize();

            Players = new List<YargPlayer>();

            Shader.SetGlobalFloat("_IsFading", 1f);
        }

        protected override void SingletonDestroy()
        {
            SettingsManager.SaveSettings();
            PlayerContainer.SaveProfiles();
            CustomContentManager.SaveAll();

            ReplayContainer.Destroy();
            InputManager.Destroy();
            PlayerContainer.Destroy();
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

        public void SetSongList(SongCache cache)
        {
            SongContainer = new(cache);
            SortedSongs = new(SongContainer);
        }
    }
}
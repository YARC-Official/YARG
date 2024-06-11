using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Audio.BASS;
using YARG.Core;
using YARG.Core.Logging;
using YARG.Core.Audio;
using YARG.Core.Song;
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
        private const string OFFLINE_ARG = "-offline";

        public List<YargPlayer> Players { get; private set; }

        public static IReadOnlyList<string> CommandLineArguments { get; private set; }

        public static bool OfflineMode { get; private set; }

        public static PersistentState State = PersistentState.Default;

        public SceneIndex CurrentScene { get; private set; } = SceneIndex.Persistent;

        public string CurrentVersion { get; private set; } = "v0.12.3";

        protected override void SingletonAwake()
        {
            CurrentVersion = LoadVersion();
            YargLogger.LogFormatInfo("YARG {0}", CurrentVersion);

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

            // Initialize important classes
            ReplayContainer.Init();
            ScoreContainer.Init();
            PlaylistContainer.Initialize();
            CustomContentManager.Initialize();

            // Check for offline mode
            OfflineMode = CommandLineArguments.Contains(OFFLINE_ARG);
            if (OfflineMode)
            {
                YargLogger.LogInfo("Playing in offline mode");
            }

            int profileCount = PlayerContainer.LoadProfiles();
            YargLogger.LogFormatInfo("Loaded {0} profiles", profileCount);

            int savedCount = PlayerContainer.SaveProfiles();
            YargLogger.LogFormatInfo("Saved {0} profiles", savedCount);

            GlobalAudioHandler.Initialize<BassAudioManager>();

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

#if UNITY_EDITOR
        // For respecting the editor's mute button
        private bool previousMute = false;
        private void Update()
        {
            bool muted = UnityEditor.EditorUtility.audioMasterMute;
            if (muted != previousMute)
            {
                GlobalAudioHandler.SetMasterVolume(muted ? 0 : SettingsManager.Settings.MasterMusicVolume.Value);
                previousMute = muted;
            }
        }
#endif

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
            GlobalAudioHandler.Close();
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

        private string LoadVersion()
        {
#if UNITY_EDITOR
            return LoadVersionFromGit();
#elif YARG_TEST_BUILD || YARG_NIGHTLY_BUILD
            var versionFile = Resources.Load<TextAsset>("version");
            return versionFile.text;
#else
            return CurrentVersion;
#endif
        }

        public static string LoadVersionFromGit()
        {
            var process = new Process();
            process.StartInfo.FileName = "git";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;

            // Branch
            process.StartInfo.Arguments = "rev-parse --abbrev-ref HEAD";
            process.Start();
            string branch = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            // Commit Count
            process.StartInfo.Arguments = "rev-list --count HEAD";
            process.Start();
            string commitCount = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            // Commit
            process.StartInfo.Arguments = "rev-parse --short HEAD";
            process.Start();
            string commit = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            #if YARG_NIGHTLY_BUILD
            return $"b{commitCount} ({commit})";
            #else
            return $"{branch} b{commitCount} ({commit})";
            #endif
        }
    }
}
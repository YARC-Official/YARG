using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using YARG.Audio.Headless;
using YARG.Audio.BASS;
using YARG.Core;
using YARG.Core.Logging;
using YARG.Core.Audio;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Input;
using YARG.Integration;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Menu.ScoreScreen;
using YARG.Player;
using YARG.Playlists;
using YARG.Replays;
using YARG.Scores;
using YARG.Settings;
using YARG.Settings.Customization;
using YARG.Song;
using YARG.Logging;
using YARG.Logging.Unity;

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
        public List<YargPlayer> Players { get; private set; }

        public static bool OfflineMode    { get; private set; }
        public static bool VerboseReplays { get; private set; }

        public static string PersistentDataPathOverride { get; private set; }

        public static PersistentState State = PersistentState.Default;

        public SceneIndex CurrentScene { get; private set; } = SceneIndex.Persistent;

        public string CurrentVersion { get; private set; } = "v0.13.1";

        private bool _isHeadlessEnvironment;
        private bool _inputInitialized;

        public bool IsHeadlessEnvironment => _isHeadlessEnvironment;

        protected override void SingletonAwake()
        {
            CurrentVersion = LoadVersion();
            YargLogger.LogFormatInfo("YARG {0}", CurrentVersion);

            _isHeadlessEnvironment = DetectHeadlessEnvironment();
            if (_isHeadlessEnvironment)
            {
                UnityInternalLogWrapper.SetLogFilter(HeadlessLogFilter.ShouldSuppress);
                YargLogger.LogInfo("[DedicatedServer] Headless environment detected; skipping presentation subsystems.");
            }

            // Command line arguments

            if (CommandLineArgs.Offline)
            {
                OfflineMode = true;
                YargLogger.LogInfo("Playing in offline mode");
            }

            if (CommandLineArgs.VerboseReplays)
            {
                VerboseReplays = true;
                YargLogger.LogInfo("Verbose replays enabled");
            }

            if (!string.IsNullOrEmpty(CommandLineArgs.DownloadLocation))
            {
                PathHelper.SetPathsFromDownloadLocation(CommandLineArgs.DownloadLocation);
            }

            // TODO: Actually respect the PersistentDataPath arg

            // Initialize important classes

            ReplayContainer.Init();
            ScoreContainer.Init();
            PlaylistContainer.Initialize();
            CustomContentManager.Initialize();
            LocalizationManager.Initialize(CommandLineArgs.Language);

            int profileCount = PlayerContainer.LoadProfiles();
            YargLogger.LogFormatInfo("Loaded {0} profiles", profileCount);

            int savedCount = PlayerContainer.SaveProfiles(false);
            YargLogger.LogFormatInfo("Saved {0} profiles", savedCount);

            if (_isHeadlessEnvironment)
            {
                GlobalAudioHandler.Initialize<NullAudioManager>();
            }
            else
            {
                GlobalAudioHandler.Initialize<BassAudioManager>();
            }

            Players = new List<YargPlayer>();

            // Set alpha fading (on the tracks) to on
            // (this is mostly for the editor, but just in case)
            if (!_isHeadlessEnvironment)
            {
                Shader.SetGlobalFloat("_IsFading", 1f);
            }
        }

        private void Start()
        {
            SettingsManager.LoadSettings();
            if (!_isHeadlessEnvironment)
            {
                InputManager.Initialize();
                _inputInitialized = true;
                LoadScene(SceneIndex.Menu);
            }
            else
            {
                YargLogger.LogInfo("[DedicatedServer] Menu scene load skipped in headless mode.");
            }
        }

#if UNITY_EDITOR

        // For respecting the editor's mute button
        private bool _previousMute;

        private void Update()
        {
            bool muted = UnityEditor.EditorUtility.audioMasterMute;
            if (muted != _previousMute)
            {
                GlobalAudioHandler.SetMasterVolume(muted ? 0 : SettingsManager.Settings.MasterMusicVolume.Value);
                _previousMute = muted;
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
            if (_inputInitialized)
            {
                InputManager.Destroy();
            }
            PlayerContainer.Destroy();
            GlobalAudioHandler.Close();

            if (_isHeadlessEnvironment)
            {
                UnityInternalLogWrapper.SetLogFilter(null);
            }

#if UNITY_EDITOR
            // Set alpha fading (on the tracks) to off
            if (!_isHeadlessEnvironment)
            {
                Shader.SetGlobalFloat("_IsFading", 0f);
            }
#endif
        }

        private void LoadSceneAdditive(SceneIndex scene)
        {
            try
            {
                UnityEngine.Debug.Log($"[GlobalVariables] LoadSceneAdditive starting for scene: {scene}");
                
                var asyncOp = SceneManager.LoadSceneAsync((int) scene, LoadSceneMode.Additive);

                if (asyncOp == null)
                {
                    UnityEngine.Debug.LogError($"[GlobalVariables] LoadSceneAsync returned null for scene: {scene}");
                    return;
                }

                CurrentScene = scene;
                GameStateFetcher.SetSceneIndex(scene);

                asyncOp.completed += _ =>
                {
                    try
                    {
                        UnityEngine.Debug.Log($"[GlobalVariables] Scene {scene} loaded successfully, setting as active scene");
                        
                        // When complete, set the newly loaded scene to the active one
                        SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex((int) scene));
                        
                        // Skip Navigator if it's null (can happen during multiplayer scene transitions)
                        if (Navigator.Instance != null)
                        {
                            Navigator.Instance.DisableMenuInputs = false;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        UnityEngine.Debug.LogError($"[GlobalVariables] Exception in asyncOp.completed callback: {ex.Message}\n{ex.StackTrace}");
                    }
                };
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[GlobalVariables] Exception in LoadSceneAdditive: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        public void LoadScene(SceneIndex scene)
        {
            try
            {
                UnityEngine.Debug.Log($"[GlobalVariables] LoadScene called for scene: {scene}, CurrentScene: {CurrentScene}");
                
                // Skip Navigator if it's null (can happen during multiplayer scene transitions)
                if (Navigator.Instance != null)
                {
                    Navigator.Instance.DisableMenuInputs = true;
                }
                
                // Unload the current scene and load in the new one, or just load in the new one
                if (CurrentScene != SceneIndex.Persistent)
                {
                    UnityEngine.Debug.Log($"[GlobalVariables] Unloading current scene: {CurrentScene}");
                    
                    // Unload the current scene
                    var asyncOp = SceneManager.UnloadSceneAsync((int) CurrentScene);

                    // Then load the new scene
                    if (asyncOp != null)
                    {
                        UnityEngine.Debug.Log("[GlobalVariables] UnloadSceneAsync started successfully, will load new scene when complete");
                        asyncOp.completed += _ => LoadSceneAdditive(scene);
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("[GlobalVariables] UnloadSceneAsync returned null - waiting one frame before loading");
                        // Wait one frame to ensure old scene objects are destroyed
                        StartCoroutine(LoadSceneAfterFrame(scene));
                    }
                }
                else
                {
                    UnityEngine.Debug.Log("[GlobalVariables] CurrentScene is Persistent, loading additively immediately");
                    LoadSceneAdditive(scene);
                }
                GC.Collect();
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[GlobalVariables] Exception in LoadScene: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }
        
        private System.Collections.IEnumerator LoadSceneAfterFrame(SceneIndex scene)
        {
            // Wait until the old scene is actually unloaded
            // UnloadSceneAsync returning null means the scene is already unloading or invalid
            // We need to wait until no scenes except Persistent are loaded
            while (SceneManager.sceneCount > 1)
            {
                yield return null;
            }
            
            // Additional wait: Ensure MenuManager singleton is destroyed before loading new scene
            // This prevents old menu objects from persisting and responding to network events
            while (Menu.MenuManager.Instance != null)
            {
                yield return null;
            }
            
            UnityEngine.Debug.Log($"[GlobalVariables] Old scene unloaded and MenuManager destroyed, now loading scene: {scene}");
            LoadSceneAdditive(scene);
        }

        // Due to the preprocessor, it doesn't know that an instance variable is being used
        // ReSharper disable once MemberCanBeMadeStatic.Local
        private string LoadVersion()
        {
#if UNITY_EDITOR
            return LoadVersionFromGit();
#elif YARG_TEST_BUILD || YARG_NIGHTLY_BUILD
            var versionFile = Resources.Load<TextAsset>("version");
            if (versionFile != null)
            {
                return versionFile.text;
            }
            else
            {
                return CurrentVersion;
            }
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
            process.StartInfo.CreateNoWindow = true;

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

        private static bool DetectHeadlessEnvironment()
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                return true;
            }

            if (CommandLineArgs.DedicatedServer)
            {
                return true;
            }

            string env = Environment.GetEnvironmentVariable("YARG_DEDICATED") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(env))
            {
                env = env.Trim();
                if (env.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                    env.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                    env.Equals("yes", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

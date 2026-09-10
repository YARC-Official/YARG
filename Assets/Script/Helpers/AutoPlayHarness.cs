#if YARG_TEST_BUILD
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using YARG.Core;
using YARG.Core.Engine;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Gameplay;
using YARG.Player;
using YARG.Song;

namespace YARG.Helpers
{
    /// <summary>
    ///     Test-build harness for cross-platform render comparisons and for
    ///     recording the GPU states a song uses. Drop an <c>autoplay.txt</c>
    ///     into the persistent data folder; once the main menu is ready the
    ///     song starts by itself:
    ///     <list type="bullet">
    ///     <item><c>song=</c> title fragment (required)</item>
    ///     <item><c>time=</c> song seconds at which <c>out=</c> (autoplay.png) is
    ///     written next to the trigger; desktop players quit afterwards. Every
    ///     platform renders the same frame, so captures compare pixel for pixel</item>
    ///     <item><c>instrument=</c> (FiveFretGuitar) and <c>difficulty=</c> (Expert)</item>
    ///     <item><c>bot=0</c> plays a human profile with no input, so every note is missed</item>
    ///     <item><c>speed=</c> song speed (1)</item>
    ///     <item><c>starpower=</c> song seconds after which the bot activates star power whenever it can</item>
    ///     <item><c>pause=</c> song seconds at which the game pauses for two seconds</item>
    ///     <item><c>mode=full</c> plays through to the score screen and back to the menu instead of capturing</item>
    ///     <item><c>repeat=</c> how many times a full run plays the song in one process (1)</item>
    ///     </list>
    /// </summary>
    public class AutoPlayHarness : MonoBehaviour
    {
        private const string TRIGGER_FILE = "autoplay.txt";
        private const float QUIT_DELAY = 2f;
        private const float PAUSE_SECONDS = 2f;
        private const float SCORE_SCREEN_SECONDS = 4f;

        private string     _songQuery;
        private double     _captureTime;
        private string     _outputName = "autoplay.png";
        private Instrument _instrument = Instrument.FiveFretGuitar;
        private Difficulty _difficulty = Difficulty.Expert;
        private bool       _bot = true;
        private float      _speed = 1f;
        private double     _starPowerTime = -1;
        private double     _pauseTime = -1;
        private bool       _full;
        private int        _repeat = 1;

        private bool  _started;
        private bool  _captured;
        private float _capturedAt;
        private bool  _starPowerHeld;
        private float _pausedAt = -1;
        private bool  _pauseDone;
        private float _scoreShownAt = -1;
        private bool  _leftScoreScreen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string trigger = Path.Combine(PathHelper.PersistentDataPath, TRIGGER_FILE);
            if (!File.Exists(trigger))
            {
                return;
            }

            var go = new GameObject("AutoPlayHarness");
            DontDestroyOnLoad(go);
            var harness = go.AddComponent<AutoPlayHarness>();
            harness.Parse(File.ReadAllLines(trigger));

            // One shot: the next launch behaves normally
            File.Delete(trigger);
            YargLogger.LogInfo($"[AUTOPLAY] armed: song '{harness._songQuery}' on {harness._instrument} " +
                $"{harness._difficulty}, {(harness._bot ? "bot" : "no input")}, x{harness._speed}, " +
                $"{(harness._full ? "full run" : $"capture at {harness._captureTime:F2} s")}");
        }

        private void Parse(string[] lines)
        {
            foreach (string line in lines)
            {
                int split = line.IndexOf('=');
                if (split < 0)
                {
                    continue;
                }

                string key = line[..split].Trim();
                string value = line[(split + 1)..].Trim();
                switch (key)
                {
                    case "song":
                        _songQuery = value;
                        break;
                    case "time":
                        _captureTime = ParseSeconds(value);
                        break;
                    case "out":
                        _outputName = value;
                        break;
                    case "instrument":
                        Enum.TryParse(value, true, out _instrument);
                        break;
                    case "difficulty":
                        Enum.TryParse(value, true, out _difficulty);
                        break;
                    case "bot":
                        _bot = value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "speed":
                        _speed = (float) ParseSeconds(value);
                        break;
                    case "starpower":
                        _starPowerTime = ParseSeconds(value);
                        break;
                    case "pause":
                        _pauseTime = ParseSeconds(value);
                        break;
                    case "mode":
                        _full = value.Equals("full", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "repeat":
                        int.TryParse(value, out _repeat);
                        break;
                }
            }
        }

        private static double ParseSeconds(string value)
        {
            double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double result);
            return result;
        }

        private void Update()
        {
            if (!_started)
            {
                // GameManager.Awake looks up the song's source name, so the
                // source table must have finished loading (or fallen back)
                if (SceneManager.GetActiveScene().buildIndex != (int) SceneIndex.Menu ||
                    SongContainer.Count == 0 || LoadingScreen.IsActive || SongSources.Default == null)
                {
                    return;
                }

                _started = true;
                StartSong();
                return;
            }

            if (_full && UpdateFullRun())
            {
                return;
            }

            if (_captured)
            {
                if (Time.realtimeSinceStartup - _capturedAt > QUIT_DELAY)
                {
                    if (!Application.isMobilePlatform)
                    {
                        Application.Quit();
                    }

                    Destroy(gameObject);
                }

                return;
            }

            var gameManager = FindAnyObjectByType<GameManager>();
            if (gameManager == null || !gameManager.IsSongStarted)
            {
                return;
            }

            DriveSong(gameManager);

            if (_full || gameManager.Paused || gameManager.SongTime < _captureTime)
            {
                return;
            }

            _captured = true;
            _capturedAt = Time.realtimeSinceStartup;
            StartCoroutine(Capture(Path.Combine(PathHelper.PersistentDataPath, _outputName), gameManager.SongTime));
        }

        // Walks the score screen and back to the menu once the song is over;
        // true while that is in progress
        private bool UpdateFullRun()
        {
            int scene = SceneManager.GetActiveScene().buildIndex;
            if (scene == (int) SceneIndex.Score)
            {
                if (_scoreShownAt < 0)
                {
                    _scoreShownAt = Time.realtimeSinceStartup;
                }
                else if (!_leftScoreScreen && Time.realtimeSinceStartup - _scoreShownAt > SCORE_SCREEN_SECONDS)
                {
                    _leftScoreScreen = true;
                    GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                }

                return true;
            }

            if (scene == (int) SceneIndex.Menu && _leftScoreScreen)
            {
                if (--_repeat > 0)
                {
                    // Play again from the menu, in this same process
                    YargLogger.LogInfo($"[AUTOPLAY] playing again ({_repeat} more after this)");
                    _started = false;
                    _leftScoreScreen = false;
                    _scoreShownAt = -1;
                    _starPowerHeld = false;
                    _pausedAt = -1;
                    _pauseDone = false;
                    return true;
                }

                YargLogger.LogInfo("[AUTOPLAY] run complete");
                Destroy(gameObject);
                return true;
            }

            return false;
        }

        private void DriveSong(GameManager gameManager)
        {
            double time = gameManager.SongTime;

            if (_starPowerTime >= 0 && !_starPowerHeld && time >= _starPowerTime)
            {
                _starPowerHeld = true;

                // Bots take no input, but the engine activates star power on
                // its own while it sees the button held and has enough
                var held = typeof(BaseEngine).GetProperty("IsStarPowerInputActive",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var setter = held?.GetSetMethod(true);
                foreach (var player in gameManager.Players)
                {
                    setter?.Invoke(player.BaseEngine, new object[] { true });
                }

                YargLogger.LogInfo($"[AUTOPLAY] holding star power from {time:F2} s ({(setter != null ? "ok" : "no setter")})");
            }

            if (_pauseTime < 0 || _pauseDone)
            {
                return;
            }

            if (_pausedAt < 0)
            {
                if (time >= _pauseTime && !gameManager.Paused)
                {
                    _pausedAt = Time.realtimeSinceStartup;
                    gameManager.Pause();
                    YargLogger.LogInfo($"[AUTOPLAY] paused at {time:F2} s");
                }
            }
            else if (Time.realtimeSinceStartup - _pausedAt > PAUSE_SECONDS)
            {
                _pauseDone = true;
                gameManager.Resume();
                YargLogger.LogInfo("[AUTOPLAY] resumed");
            }
        }

        // ScreenCapture.CaptureScreenshot resolves paths differently per
        // platform (relative to persistentDataPath on phones), so read the
        // frame back and write the file ourselves
        private System.Collections.IEnumerator Capture(string outputPath, double songTime)
        {
            yield return new WaitForEndOfFrame();

            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            Destroy(texture);

            YargLogger.LogInfo($"[AUTOPLAY] captured {outputPath} at song time {songTime:F3} s, " +
                $"{Screen.width}x{Screen.height}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            DumpRenderState();
        }

        // Everything that could make the same venue render differently per platform
        private static void DumpRenderState()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            YargLogger.LogInfo($"[RENDER] {SystemInfo.graphicsDeviceType} shaderLevel={SystemInfo.graphicsShaderLevel} " +
                $"pipeline={(pipeline != null ? pipeline.name : "?")} hdr={pipeline?.supportsHDR} " +
                $"lightsMode={pipeline?.additionalLightsRenderingMode} maxLights={pipeline?.maxAdditionalLightsCount} " +
                $"colorSpace={QualitySettings.activeColorSpace} mipLimit={QualitySettings.globalTextureMipmapLimit}");
            YargLogger.LogInfo($"[RENDER] ambient mode={RenderSettings.ambientMode} intensity={RenderSettings.ambientIntensity} " +
                $"light={RenderSettings.ambientLight} reflection={RenderSettings.defaultReflectionMode}/{RenderSettings.reflectionIntensity} " +
                $"fog={RenderSettings.fog} sun={(RenderSettings.sun != null ? RenderSettings.sun.name : "none")}");

            var unitProperty = typeof(Light).GetProperty("lightUnit");
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                string unit = unitProperty != null ? unitProperty.GetValue(light)?.ToString() : "n/a";
                YargLogger.LogInfo($"[RENDER] light '{light.name}' {light.type} on={light.isActiveAndEnabled} " +
                    $"intensity={light.intensity} unit={unit} range={light.range} spot={light.spotAngle} " +
                    $"color={light.color} mask={light.cullingMask} shadows={light.shadows} scene={light.gameObject.scene.name}");
            }

            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                var data = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                var target = camera.targetTexture;
                YargLogger.LogInfo($"[RENDER] camera '{camera.name}' on={camera.isActiveAndEnabled} hdr={camera.allowHDR} " +
                    $"post={data?.renderPostProcessing} volumes={data?.volumeLayerMask.value} aa={data?.antialiasing} " +
                    $"depth={data?.requiresDepthTexture} mask={camera.cullingMask} " +
                    $"target={(target != null ? $"{target.width}x{target.height} {target.graphicsFormat}" : "screen")}");
            }

            foreach (var volume in UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include))
            {
                YargLogger.LogInfo($"[RENDER] volume '{volume.name}' on={volume.isActiveAndEnabled} global={volume.isGlobal} " +
                    $"weight={volume.weight} layer={volume.gameObject.layer} profile={(volume.sharedProfile != null ? volume.sharedProfile.name : "none")}");
            }
        }

        private void StartSong()
        {
            SongEntry song = null;
            foreach (var entry in SongContainer.UnfilteredSongs)
            {
                if (entry.Name.ToString().Contains(_songQuery, StringComparison.OrdinalIgnoreCase))
                {
                    song = entry;
                    break;
                }
            }

            if (song == null)
            {
                YargLogger.LogError($"[AUTOPLAY] no song matches '{_songQuery}'");
                Destroy(gameObject);
                return;
            }

            // One harness profile plays; everyone else sits out. A bot hits
            // every note, a human profile with no device misses every note
            var gameMode = _instrument.ToNativeGameMode();
            string profileName = _bot ? "AutoPlay Bot" : "AutoPlay Human";
            YargProfile profile = null;
            foreach (var candidate in PlayerContainer.Profiles)
            {
                if (candidate.Name == profileName && candidate.GameMode == gameMode)
                {
                    profile = candidate;
                    break;
                }
            }

            if (profile == null)
            {
                profile = new YargProfile
                {
                    Name = profileName,
                    NoteSpeed = 5,
                    HighwayLength = 1,
                    GameMode = gameMode,
                    IsBot = _bot,
                };
                PlayerContainer.AddProfile(profile);
            }

            profile.CurrentInstrument = _instrument;
            profile.CurrentDifficulty = _difficulty;

            var harnessPlayer = PlayerContainer.GetPlayerFromProfile(profile) ??
                PlayerContainer.CreatePlayerFromProfile(profile, false);
            foreach (var player in PlayerContainer.Players)
            {
                player.SittingOut = player != harnessPlayer;
            }

            var state = PersistentState.Default;
            state.CurrentSong = song;
            state.SongSpeed = _speed;
            GlobalVariables.State = state;

            YargLogger.LogInfo($"[AUTOPLAY] starting '{song.Name}' by {song.Artist}");
            GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
        }
    }
}
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using YARG.Core.Logging;
using Object = UnityEngine.Object;

namespace YARG.Helpers
{
    /// <summary>
    ///     Pre-creates the GPU pipeline states a play session needs so they
    ///     are not compiled mid-song, which stutters on phones. Mobile players
    ///     load a state collection recorded on the same platform, graphics API
    ///     and quality level from StreamingAssets and warm it up at startup,
    ///     then again for every song once its venue has loaded so the venue
    ///     bundle's shaders are covered too. Test builds keep tracing the
    ///     states they use, save the collection to the data folder for the
    ///     next recording and report which states the shipped one is missing.
    /// </summary>
    public static class GraphicsStateWarmup
    {
        private const string FOLDER = "graphicsstate";
        private const float VENUE_WAIT_SECONDS = 60f;

        private static UniTask _pending = UniTask.CompletedTask;
        private static GraphicsStateCollection _trace;
        private static string _tracePath;

        // One collection per platform, graphics API and quality level: the
        // quality level sets the anti-aliasing, which is part of every state
        private static string FileName => $"{Application.platform}-{SystemInfo.graphicsDeviceType}-" +
            $"{QualitySettings.names[QualitySettings.GetQualityLevel()]}.graphicsstate";

        private static string ShippedPath => Path.Combine(Application.streamingAssetsPath, FOLDER, FileName);
        private static string TracePath => Path.Combine(PathHelper.PersistentDataPath, FOLDER, FileName);

        // Start on the shipped states while the menus load, so the first
        // song has less left to create
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void WarmUpAtStartup()
        {
            if (Application.isMobilePlatform)
            {
                WarmUpAsync(SettingsLoaded()).Forget();
            }
        }

        // The settings pick the quality level, and with it the collection
        private static async UniTask SettingsLoaded()
        {
            await UniTask.WaitUntil(() => Settings.SettingsManager.Settings != null);
            await UniTask.NextFrame();
        }

        /// <summary>
        ///     Creates every recorded state that does not exist yet once the
        ///     venue has loaded. Awaiting this holds the song under the
        ///     loading screen until the states exist.
        /// </summary>
        public static async UniTask WarmUpAsync(UniTask venueLoaded)
        {
            if (!Application.isMobilePlatform)
            {
                return;
            }

            // A venue that never finishes loading must not hold the song
            await UniTask.WhenAny(venueLoaded, UniTask.Delay(TimeSpan.FromSeconds(VENUE_WAIT_SECONDS)));

            // One pass at a time, so a startup pass and a song's never overlap
            // (the task is awaited by every caller that finds it pending)
            await _pending;
            _pending = WarmUpPass().Preserve();
            await _pending;
        }

        private static async UniTask WarmUpPass()
        {
            string path = ShippedPath;
            if (!File.Exists(path))
            {
                YargLogger.LogFormatInfo("No graphics state collection shipped at {0}", path);
                return;
            }

            // A fresh collection each pass: states created by an earlier pass
            // are found again instantly, and shaders loaded since (the venue
            // bundle's) resolve this time
            var collection = new GraphicsStateCollection();
            try
            {
                if (!collection.LoadFromFile(path))
                {
                    YargLogger.LogFormatWarning("Failed to load the graphics state collection at {0}", path);
                    return;
                }

                if (collection.runtimePlatform != Application.platform ||
                    collection.graphicsDeviceType != SystemInfo.graphicsDeviceType)
                {
                    YargLogger.LogFormatWarning("Graphics state collection is for {0}/{1}, not {2}/{3}",
                        collection.runtimePlatform, collection.graphicsDeviceType,
                        Application.platform, SystemInfo.graphicsDeviceType);
                    return;
                }

                var stopwatch = Stopwatch.StartNew();
                await collection.WarmUp();

                YargLogger.LogInfo($"Warmed up {collection.completedWarmupCount} of " +
                    $"{collection.totalGraphicsStateCount} graphics states across {collection.variantCount} " +
                    $"shader variants in {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Graphics state warm-up failed");
            }
            finally
            {
                Object.Destroy(collection);
            }
        }

#if YARG_TEST_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartTrace()
        {
            if (!Application.isMobilePlatform)
            {
                return;
            }

            BeginTrace();

            SceneManager.sceneUnloaded += _ => SaveTrace();
            Application.quitting += SaveTrace;

            var saver = new GameObject("Graphics State Trace")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(saver);
            saver.AddComponent<TraceSaver>();
        }

        private static void BeginTrace()
        {
            _tracePath = TracePath;
            _trace = new GraphicsStateCollection();

            // Keep accumulating on top of the previous session's recording
            if (File.Exists(_tracePath) && !_trace.LoadFromFile(_tracePath))
            {
                Object.Destroy(_trace);
                _trace = new GraphicsStateCollection();
            }

            _trace.BeginTrace();
            YargLogger.LogInfo($"Graphics state tracing {(_trace.isTracing ? "started" : "NOT started")} " +
                $"into {Path.GetFileName(_tracePath)} (parallel PSO creation supported: " +
                $"{SystemInfo.supportsParallelPSOCreation})");
        }

        private static void SaveTrace()
        {
            if (_trace == null || !_trace.isTracing)
            {
                return;
            }

            _trace.EndTrace();
            Directory.CreateDirectory(Path.GetDirectoryName(_tracePath));
            _trace.SaveToFile(_tracePath);
            YargLogger.LogInfo($"Saved {_trace.totalGraphicsStateCount} graphics states across " +
                $"{_trace.variantCount} shader variants to {_tracePath}");
            ReportMissingStates();
            _trace.BeginTrace();
        }

        // The quality level is part of every state and names the file, so
        // a change (the settings loading, the user toggling it) ends one
        // recording and starts the next
        private static void RestartTraceForQualityChange()
        {
            SaveTrace();
            _trace.EndTrace();
            Object.Destroy(_trace);
            BeginTrace();
        }

        // Which traced states the shipped collection lacks: zero means the
        // recording covers everything this session drew
        private static void ReportMissingStates()
        {
            if (!File.Exists(ShippedPath))
            {
                return;
            }

            var shipped = new GraphicsStateCollection();
            try
            {
                if (!shipped.LoadFromFile(ShippedPath))
                {
                    return;
                }

                var variants = new List<GraphicsStateCollection.ShaderVariant>();
                _trace.GetVariants(variants);

                var traced = new List<GraphicsStateCollection.GraphicsState>();
                var known = new List<GraphicsStateCollection.GraphicsState>();
                int missingStates = 0;
                foreach (var variant in variants)
                {
                    traced.Clear();
                    known.Clear();
                    _trace.GetGraphicsStatesForVariant(variant, traced);
                    shipped.GetGraphicsStatesForVariant(variant, known);

                    int missing = traced.Count - known.Count;
                    if (missing <= 0)
                    {
                        continue;
                    }

                    missingStates += missing;
                    var keywords = new List<string>();
                    foreach (var keyword in variant.keywords)
                    {
                        keywords.Add(keyword.name);
                    }

                    string shader = variant.shader != null ? variant.shader.name : "(unloaded shader)";
                    YargLogger.LogInfo($"[WARMUP] not shipped: {missing} state(s) of {shader} pass " +
                        $"{variant.passId.SubshaderIndex}/{variant.passId.PassIndex} [{string.Join(" ", keywords)}]");
                }

                YargLogger.LogInfo($"[WARMUP] the shipped collection lacks {missingStates} of the " +
                    $"{_trace.totalGraphicsStateCount} traced states");
            }
            finally
            {
                Object.Destroy(shipped);
            }
        }

        private class TraceSaver : MonoBehaviour
        {
            private int _qualityLevel;

            private void Awake()
            {
                _qualityLevel = QualitySettings.GetQualityLevel();
            }

            private void Update()
            {
                int level = QualitySettings.GetQualityLevel();
                if (level == _qualityLevel)
                {
                    return;
                }

                _qualityLevel = level;
                RestartTraceForQualityChange();
            }

            private void OnApplicationPause(bool paused)
            {
                if (paused)
                {
                    SaveTrace();
                }
            }

            private void OnApplicationQuit()
            {
                SaveTrace();
            }
        }
#endif
    }
}

#if YARG_TEST_BUILD && !UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Gameplay;

namespace YARG.Helpers
{
    /// <summary>
    ///     Logs every frame that takes far longer than its neighbours, with
    ///     the song position and where the time went (main thread, waiting
    ///     for the render thread, render thread, GPU), so a stutter reported
    ///     from a device can be matched to what was happening in the chart at
    ///     that moment and to the thread that stalled. Development builds add
    ///     the profiler markers that took the longest in the hitch frame.
    /// </summary>
    internal static class FrameHitchLogger
    {
        private const float HITCH_SECONDS = 0.12f;
        private const int TIMING_FRAMES = 12;
        private const int DETAIL_DELAY_FRAMES = 8;
        private const int TOP_MARKERS = 12;
        private const double MARKER_FLOOR_MS = 2.0;
        private const double GPU_PROGRAM_FLOOR_MS = 5.0;
        private const string GPU_PROGRAM_MARKER = "CreateGpuProgram";

        // Markers that only restate the frame length or sum idle worker threads
        private static readonly HashSet<string> NOISE_MARKERS = new()
        {
            "Idle", "Main Thread", "PlayerLoop", "Semaphore.WaitForSignal", "WaitForTargetFPS",
            "CPU Total Frame Time", "Gfx.WaitForGfxCommandsFromMainThread", "GfxResource.Register",
            "GfxResource.Unregister",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            if (!Application.isMobilePlatform)
            {
                return;
            }

            YargLogger.LogInfo($"[GFX] {SystemInfo.graphicsDeviceName} / {SystemInfo.graphicsDeviceType} " +
                $"{SystemInfo.graphicsDeviceVersion}; screen {Screen.width}x{Screen.height} @ {Screen.dpi} dpi; " +
                $"quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}; " +
                $"parallel PSO creation {SystemInfo.supportsParallelPSOCreation}; " +
                $"frame timing stats {FrameTimingManager.IsFeatureEnabled()}");

            var logger = new GameObject("Frame Hitch Logger")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(logger);
            logger.AddComponent<Watcher>();
        }

        private sealed class Watcher : MonoBehaviour
        {
            private readonly FrameTiming[] _timings = new FrameTiming[TIMING_FRAMES];

            private int _hitchFrame = -1;

            private void Update()
            {
                // Captured every frame so the hitch frame's own timings are in
                FrameTimingManager.CaptureFrameTimings();

                if (_hitchFrame >= 0 && Time.frameCount - _hitchFrame >= DETAIL_DELAY_FRAMES)
                {
                    LogDetail();
                    _hitchFrame = -1;
                }

                LogGpuProgramCreation();

                float frame = Time.unscaledDeltaTime;
                if (frame < HITCH_SECONDS)
                {
                    return;
                }

                YargLogger.LogInfo($"[HITCH] {frame * 1000f:0} ms frame ({Where()}, frame {Time.frameCount}){TopMarkers(frame)}");
                _hitchFrame = Time.frameCount;
            }

            private static string Where()
            {
                var manager = Object.FindAnyObjectByType<GameManager>();
                return manager != null && manager.IsSongStarted
                    ? $"song {manager.SongTime:0.00}s"
                    : "outside gameplay";
            }

#if DEVELOPMENT_BUILD
            // Every timing marker the profiler knows, so a hitch can name
            // what ran: the recorders hold the previous frame's totals
            private readonly List<(string name, ProfilerRecorder recorder)> _recorders = new();
            private readonly List<(string name, double ms)> _slowest = new();
            private readonly StringBuilder _builder = new();
            private ProfilerRecorder _gpuPrograms;

            private void Start()
            {
                var handles = new List<ProfilerRecorderHandle>();
                ProfilerRecorderHandle.GetAvailable(handles);
                foreach (var handle in handles)
                {
                    var description = ProfilerRecorderHandle.GetDescription(handle);
                    if (description.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds ||
                        NOISE_MARKERS.Contains(description.Name))
                    {
                        continue;
                    }

                    var recorder = new ProfilerRecorder(handle, 1,
                        ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
                    recorder.Start();
                    _recorders.Add((description.Name, recorder));
                }

                _gpuPrograms = _recorders.Find(entry => entry.name == GPU_PROGRAM_MARKER).recorder;
                YargLogger.LogInfo($"[HITCH] recording {_recorders.Count} profiler markers");
            }

            // Shader compiles below the hitch threshold still say where the
            // pre-warming fell short, so any frame spending real time on
            // them is logged on its own
            private void LogGpuProgramCreation()
            {
                if (!_gpuPrograms.Valid)
                {
                    return;
                }

                double ms = _gpuPrograms.LastValue / 1_000_000.0;
                if (ms >= GPU_PROGRAM_FLOOR_MS)
                {
                    YargLogger.LogInfo($"[HITCH] {ms:0} ms creating GPU programs in frame {Time.frameCount - 1} ({Where()})");
                }
            }

            private void OnDestroy()
            {
                foreach (var (_, recorder) in _recorders)
                {
                    recorder.Dispose();
                }
            }

            private string TopMarkers(float frameSeconds)
            {
                // Sums over every thread, so a marker can exceed the frame;
                // anything far beyond it is a counter in disguise
                double ceilingMs = frameSeconds * 1000.0 * 4.0;

                _slowest.Clear();
                foreach (var (name, recorder) in _recorders)
                {
                    if (!recorder.Valid)
                    {
                        continue;
                    }

                    double ms = recorder.LastValue / 1_000_000.0;
                    if (ms >= MARKER_FLOOR_MS && ms <= ceilingMs)
                    {
                        _slowest.Add((name, ms));
                    }
                }

                if (_slowest.Count == 0)
                {
                    return "";
                }

                _slowest.Sort((a, b) => b.ms.CompareTo(a.ms));
                _builder.Clear();
                _builder.Append("; markers:");
                for (int i = 0; i < _slowest.Count && i < TOP_MARKERS; i++)
                {
                    _builder.Append(i == 0 ? " " : ", ");
                    _builder.Append($"{_slowest[i].name} {_slowest[i].ms:0}");
                }

                return _builder.ToString();
            }
#else
            private static string TopMarkers(float frameSeconds) => "";

            private static void LogGpuProgramCreation()
            {
            }
#endif

            // Frame timings arrive a few frames late (they include the GPU),
            // so the hitch frame is looked up afterwards: the slowest of the
            // latest frames is it
            private void LogDetail()
            {
                if (!FrameTimingManager.IsFeatureEnabled())
                {
                    return;
                }

                uint count = FrameTimingManager.GetLatestTimings((uint) _timings.Length, _timings);
                int slowest = -1;
                for (int i = 0; i < count; i++)
                {
                    if (slowest < 0 || _timings[i].cpuFrameTime > _timings[slowest].cpuFrameTime)
                    {
                        slowest = i;
                    }
                }

                if (slowest < 0)
                {
                    return;
                }

                var timing = _timings[slowest];
                YargLogger.LogInfo($"[HITCH] slowest of the last {count} frames: cpu {timing.cpuFrameTime:0} ms = " +
                    $"main thread {timing.cpuMainThreadFrameTime:0} + waiting for the render thread " +
                    $"{timing.cpuMainThreadPresentWaitTime:0}; render thread {timing.cpuRenderThreadFrameTime:0}, " +
                    $"gpu {timing.gpuFrameTime:0}");
            }
        }
    }
}
#endif

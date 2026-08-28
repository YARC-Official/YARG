#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ManagedBass;
using UnityEditor;
using UnityEngine;
using YARG.Audio.BASS;
using YARG.Core.Audio;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Input;
using YARG.Playback;
using YARG.Settings;
using YARG.Song;

namespace YARG.Editor
{
    public sealed partial class AudioDebugWindow
    {
        private const double SAMPLE_INTERVAL = 1.0 / 60.0;
        private const int MAX_SAMPLES = 1800;
        private const int MAX_DRIFT_MEASUREMENTS = 36000;
        private const int DEFAULT_BUFFER_MS = 100;
        private const int MIN_BASS_UPDATE_PERIOD_MS = 5;
        private const int MAX_BASS_UPDATE_PERIOD_MS = 100;
        private const int MAX_BASS_DEVICE_BUFFER_MS = 500;
        private const string RECENT_PATHS_KEY = "YARG_AudioDebug_RecentPaths";
        private const int MAX_RECENT_PATHS = 8;
        private const float MIN_WINDOW_WIDTH = 740f;
        private const float MIN_WINDOW_HEIGHT = 620f;
        private const float DEFAULT_WINDOW_WIDTH = 900f;
        private const float DEFAULT_WINDOW_HEIGHT = 700f;

        private enum GraphMode
        {
            PositionJitter,
            SyncConvergence,
            FrameStepDelta,
            PositionMappingStep,
            CallbackTimingStep,
            ControlHeardDelta,
            ClockDrift,
            AbsolutePosition,
            MicPitchAndHits,
            FrequencySpectrum,
            Oscilloscope
        }

        private static readonly (GraphMode Mode, string Label)[] GRAPH_MODE_ITEMS = new[]
        {
            (GraphMode.SyncConvergence, "Sync Error & Correction"),
            (GraphMode.PositionJitter, "Playback Jitter (Stability)"),
            (GraphMode.ClockDrift, "Hardware Clock Drift"),
            (GraphMode.ControlHeardDelta, "Latency Offset (Input vs Heard)"),
            (GraphMode.FrameStepDelta, "Frame Step Interval"),
            (GraphMode.PositionMappingStep, "Audio Position Step"),
            (GraphMode.CallbackTimingStep, "Buffer Callback Interval"),
            (GraphMode.AbsolutePosition, "Raw Playback Timeline"),
            (GraphMode.MicPitchAndHits, "Mic Pitch & Volume"),
            (GraphMode.FrequencySpectrum, "Frequency Spectrum (RTA)"),
            (GraphMode.Oscilloscope, "Waveform Oscilloscope")
        };

        private static readonly string[] GRAPH_MODE_LABELS = GRAPH_MODE_ITEMS.Select(i => i.Label).ToArray();

        private enum FftDisplayStyle
        {
            FilledCurve,
            RtaBars,
            Both
        }

        private enum FftScaleMode
        {
            Logarithmic,
            Linear
        }

        private struct FftBandInfo
        {
            public string Name;
            public float MinFreq;
            public float MaxFreq;
            public float CurrentDb;
            public float PeakDb;
            public Color BandColor;
        }

        private const float FFT_MIN_FREQ = 20f;
        private const float FFT_MAX_FREQ = 20000f;

        private struct PositionSample
        {
            public double RealTime;
            public double TargetTime;
            public double HeardPosition;
            public double ControlPosition;
            public double OutputFramePosition;
            public double CallbackFramesMs;
            public double CallbackElapsedMs;
            public double CallbackCorrectionMs;
            public double CallbackClockOffsetMs;
            public double HeardErrorMs;
            public double ControlErrorMs;
            public double DriftErrorMs;
            public float Adjustment;
            public AudioSynchronizer.SyncState SyncState;
            public bool IsPlaying;
        }

        private struct MicSample
        {
            public double RealTime;
            public float MidiNote;
            public float VolumeDb;
            public bool IsHit;
            public bool IsVoiced;
        }

        private enum MicVoiceCheckState
        {
            Idle,
            Recording,
            Ready,
            Playing,
            Paused
        }

        private struct WaveformBucket
        {
            public float Min;
            public float Max;
            public float Rms;
        }

        private sealed class MicSlot : IDisposable
        {
            public int Id = 1;
            public string DisplayLabel = "Mic 1";
            public Color ThemeColor = new Color(0.15f, 0.85f, 1f, 1f);
            public InputDeviceInfo? SelectedDevice;
            public MicDevice? ActiveDevice;

            // Live metering & Pitch tracking
            public float CurrentDb = -160f;
            public float PeakDb = -160f;
            public float PeakHoldDb = -160f;
            public double LastPeakHoldTime;
            public float CurrentPitchHz;
            public float CurrentMidi;
            public string CurrentNoteName = "--";
            public float CurrentCents;
            public bool IsVoiced;
            public double LastHitTime = -10.0;
            public int TotalHitCount;
            public float GateThreshold = 2f;

            // Monitoring & Vocal FX
            public float MonitoringVolume = 1f;
            public bool Mute;
            public bool Solo;

            // Stream Health
            public float Fps;
            public int FpsFrameCount;
            public double LastFpsTime;
            public double LastFrameTime;
            public double FrameIntervalMs;
            public int FramesReceived;
            public string? StatusMessage;
            public bool StatusIsError;
            public double LastStatusTime;

            // Sample History for Oscilloscope / Graph
            public readonly List<MicSample> Samples = new(MAX_SAMPLES);
            public double LastSampleTime;

            // Voice Check & Recording per-slot
            public int RecordChannelHandle;
            public float[]? RecordBuffer;
            public int RecordSampleCount;
            public float[] RecordedSamples = Array.Empty<float>();
            public WaveformBucket[]? WaveformOverview;
            public MicVoiceCheckState RecordState = MicVoiceCheckState.Idle;
            public int RecordSampleRate = 48000;
            public float RecordTargetDuration = 5f;
            public double RecordStartTime;
            public double RecordElapsedSeconds;
            public double RecordedDuration;
            public float RecordedPeakDb = -96f;
            public float RecordedRmsDb = -96f;
            public volatile bool RecordJustFinished;
            public bool RecordFx = true;
            public bool AutoPlay = true;
            public bool PlaybackLoop;
            public float PlaybackVolume = 1f;
            public StemMixer? PlaybackMixer;
            private Thread? _recordReader;
            private volatile bool _recordStopRequested;

            public void StartRecordReader()
            {
                StopRecordReader();
                int channelHandle = RecordChannelHandle;
                if (channelHandle == 0)
                {
                    return;
                }

                _recordStopRequested = false;
                _recordReader = new Thread(() => ReadRecordSamples(channelHandle))
                {
                    IsBackground = true,
                    Name = $"Mic recording {Id}"
                };
                _recordReader.Start();
            }

            private unsafe void ReadRecordSamples(int channelHandle)
            {
                while (!_recordStopRequested)
                {
                    var buf = RecordBuffer;
                    if (buf == null)
                    {
                        return;
                    }

                    int currentCount = Volatile.Read(ref RecordSampleCount);
                    int remaining = buf.Length - currentCount;
                    if (remaining <= 0)
                    {
                        RecordJustFinished = true;
                        return;
                    }

                    int bytesRead;
                    fixed (float* destination = &buf[currentCount])
                    {
                        bytesRead = Bass.ChannelGetData(channelHandle, (IntPtr) destination,
                            remaining * sizeof(float));
                    }

                    if (bytesRead > 0)
                    {
                        Volatile.Write(ref RecordSampleCount, currentCount + bytesRead / sizeof(float));
                        if (RecordSampleCount >= buf.Length)
                        {
                            RecordJustFinished = true;
                            return;
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
            }

            private void StopRecordReader()
            {
                _recordStopRequested = true;
                if (_recordReader != null && Thread.CurrentThread != _recordReader)
                {
                    _recordReader.Join();
                }

                _recordReader = null;
            }

            public void DetachRecordingChannel()
            {
                StopRecordReader();
                if (RecordChannelHandle != 0)
                {
                    if (ActiveDevice is BassMicDevice bassMic)
                    {
                        bassMic.ReleaseRecordingChannel(RecordChannelHandle);
                    }
                    else
                    {
                        Bass.StreamFree(RecordChannelHandle);
                    }
                    RecordChannelHandle = 0;
                }
            }

            public void DisposePlayback()
            {
                if (PlaybackMixer != null)
                {
                    PlaybackMixer.Dispose();
                    PlaybackMixer = null;
                }
            }

            public void Dispose()
            {
                DetachRecordingChannel();
                DisposePlayback();
                if (ActiveDevice != null)
                {
                    ActiveDevice.Dispose();
                    ActiveDevice = null;
                }
            }
        }

        private struct DriftMeasurement
        {
            public double HostSeconds;
            public double SongPositionSeconds;
            public double DriftMs;
            public double PositionStepResidualMs;
            public ulong ConsumedFrames;
            public ulong RequestedFrames;
            public uint QueuedFrames;
            public ulong PositionOutputFrame;
            public uint CallbackFrames;
            public uint CallbackElapsedFrames;
            public long CallbackCorrectionFrames;
            public long CallbackClockOffsetFrames;
            public ulong UnderrunFrames;
            public ulong UnderrunEvents;
        }

        private static readonly string[] NOTE_NAMES = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        private static readonly SongStem[] ALL_STEMS = Enum.GetValues(typeof(SongStem)).Cast<SongStem>().Where(s => s != SongStem.Master).ToArray();

    }
}

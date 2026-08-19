#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
    public sealed class AudioDebugWindow : EditorWindow
    {
        private const double SAMPLE_INTERVAL = 1.0 / 60.0;
        private const int MAX_SAMPLES = 1800;
        private const int MAX_DRIFT_MEASUREMENTS = 36000;
        private const int DEFAULT_BUFFER_MS = 100;
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

        private int _fftSizeLog = 11; // 2048 samples (1024 bins)
        private float[]? _fftBuffer;
        private float[]? _smoothedFft;
        private float[]? _peakFft;
        private float[]? _scopePcmBuffer;
        private float _scopeTimebase = 0.020f;
        private float _scopeGain = 1f;
        private float _fftSmoothingFactor = 0.75f;
        private float _fftMinDb = -96f;
        private float _fftMaxDb = 0f;
        private FftDisplayStyle _fftDisplayStyle = FftDisplayStyle.Both;
        private FftScaleMode _fftScaleMode = FftScaleMode.Logarithmic;
        private bool _fftPeakHoldEnabled = true;
        private float _fftPeakDecayRate = 25f;
        private float _dominantFrequencyHz;
        private float _dominantDb = -160f;
        private string _dominantNoteName = "--";
        private float _dominantCents;
        private float _spectralCentroidHz;
        private int _lastFftBytesRead;
        private readonly FftBandInfo[] _fftBands =
        {
            new() { Name = "Sub Bass", MinFreq = 20f, MaxFreq = 60f, CurrentDb = -96f, PeakDb = -96f, BandColor = new Color(0.75f, 0.35f, 0.95f) },
            new() { Name = "Bass", MinFreq = 60f, MaxFreq = 250f, CurrentDb = -96f, PeakDb = -96f, BandColor = new Color(0.25f, 0.60f, 0.95f) },
            new() { Name = "Low Mid", MinFreq = 250f, MaxFreq = 500f, CurrentDb = -96f, PeakDb = -96f, BandColor = new Color(0.20f, 0.85f, 0.80f) },
            new() { Name = "Mids", MinFreq = 500f, MaxFreq = 2000f, CurrentDb = -96f, PeakDb = -96f, BandColor = new Color(0.35f, 0.85f, 0.40f) },
            new() { Name = "High Mid", MinFreq = 2000f, MaxFreq = 4000f, CurrentDb = -96f, PeakDb = -96f, BandColor = new Color(0.90f, 0.80f, 0.25f) },
            new() { Name = "Presence", MinFreq = 4000f, MaxFreq = 6000f, CurrentDb = -96f, PeakDb = -96f, BandColor = new Color(0.95f, 0.55f, 0.20f) },
            new() { Name = "Brilliance", MinFreq = 6000f, MaxFreq = 20000f, CurrentDb = -96f, PeakDb = -96f, BandColor = new Color(0.95f, 0.30f, 0.35f) }
        };

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

        private readonly List<PositionSample> _samples = new(MAX_SAMPLES);
        private readonly List<MicSample> _micSamples = new(MAX_SAMPLES);
        private readonly List<InputDeviceInfo> _availableMicDevices = new();
        private readonly Dictionary<SongStem, float> _stemVolumes = new();
        private readonly Dictionary<SongStem, bool> _stemMutes = new();
        private readonly Dictionary<SongStem, bool> _stemSolos = new();
        private readonly Dictionary<SongStem, bool> _stemReverbs = new();
        private List<string> _recentPaths = new();

        private BassSong? _bassSong;
        private AudioSynchronizer? _audioSynchronizer;
        private ReadAheadStats _latestReadAheadStats;
        private bool _modelSongSync = true;
        private double _inputTimeOffset;
        private double _simulatedClockDisturbance;
        private float _simulatedClockDriftPercent;
        private float _audioCalibrationMs;

        private MicDevice? _activeMicDevice;
        private InputDeviceInfo? _selectedMicDevice;
        private double _lastMicSampleTime;
        private double _lastMicFrameTime;
        private double _micFrameIntervalMs;
        private int _micFramesReceived;
        private int _micFpsFrameCount;
        private double _lastMicFpsTime;
        private float _micFps;
        private float _micCurrentDb = -160f;
        private float _micPeakDb = -160f;
        private float _micPeakHoldDb = -160f;
        private double _lastMicPeakHoldTime;
        private float _micCurrentPitchHz;
        private float _micCurrentMidi;
        private string _micCurrentNoteName = "--";
        private float _micCurrentCents;
        private bool _micIsVoiced;
        private double _lastHitTime = -10.0;
        private int _totalHitCount;
        private float _micMonitoringVolume = 1f;
        private bool _micMonitoringEnabled;
        private string? _micStatusMessage;
        private bool _micStatusIsError;
        private double _lastMicStatusTime;

        private string _loadedSongName = "No song loaded";
        private string _sourcePath = string.Empty;

        private int _readAheadBufferMs = DEFAULT_BUFFER_MS;
        private float _playbackSpeed = 1f;
        private float _volume = 1f;

        private double _playbackClock;
        private double _lastUpdateTime;
        private double _lastSampleTime;
        private double _lastFpsUpdateTime;
        private int _fpsFrameCount;
        private float _currentFps;

        private bool _isDriftTestRunning;
        private bool _driftBaselineEstablished;
        private double _driftTestStartTime;
        private long _driftTestQpcStart;
        private double _driftInitialAudioStart;
        private double _driftElapsedHostSeconds;
        private double _driftElapsedAudioSeconds;
        private double _driftCumulativeMs;
        private double _driftRatePpm;
        private double _driftMsPerMin;
        private double _calibrationTrackLength;
        private int _driftAudioLoopCount;
        private readonly List<DriftMeasurement> _driftMeasurements = new(MAX_DRIFT_MEASUREMENTS);
        private ulong _driftStartRequestedFrames;
        private double _driftCallbackRatePpm;
        private double _driftMaxPositionStepMs;
        private int _driftLargePositionStepCount;
        private double _driftPreviousSampleTime;
        private double _driftPreviousSongPosition;
        private bool _driftPreviousPositionValid;

        private GraphMode _graphMode = GraphMode.SyncConvergence;
        private float _graphTimeWindow = 2f;
        private float _jitterScaleMs = 10f;
        private bool _autoScroll = true;
        private bool _freezeGraph;
        private double _viewEndTime = -1;
        private int _selectedBottomTab;

        private Vector2 _mainScroll;
        private Vector2 _libraryScroll;
        private string _librarySearch = string.Empty;
        private bool _showLibrarySection;
        private string? _deviceStatusMessage;
        private bool _deviceStatusIsError;
        private double _lastDeviceStatusTime;

        private bool _isScrubbing;
        private float _scrubTarget;

        private MetronomeSample _testMetronomeSound = MetronomeSample.Clap;
        private bool _metronomeLoopRunning;
        private float _metronomeBpm = 120f;
        private int _metronomeBeatsPerBar = 4;
        private int _metronomeCurrentBeat;
        private double _nextMetronomeBeatTime;
        private double _lastMetronomeClickTime;
        private bool _lastMetronomeClickIsHi;
        private float _metronomeVolume = 1f;
        private int _metronomeTargetChannel = -1;
        private bool _headphoneAuditionMode = false;
        private readonly float[] _channelPairPeaks = new float[4] { -96f, -96f, -96f, -96f };
        private readonly float[] _channelPairPeakHold = new float[4] { -96f, -96f, -96f, -96f };
        private readonly double[] _channelPairPeakHoldTime = new double[4];
        private readonly double[] _tapTimes = new double[4];
        private int _tapIndex;
        private int _totalMetronomeClicks;

        [MenuItem("Window/YARG/Audio Debug Player")]
        [MenuItem("YARG/Debug/Audio Debug Player")]
        public static void Open()
        {
            var window = GetWindow<AudioDebugWindow>("Audio Debug");
            window.minSize = new Vector2(MIN_WINDOW_WIDTH, MIN_WINDOW_HEIGHT);
            var pos = window.position;
            if (pos.height < DEFAULT_WINDOW_HEIGHT || pos.width < DEFAULT_WINDOW_WIDTH)
            {
                pos.width = Mathf.Max(pos.width, DEFAULT_WINDOW_WIDTH);
                pos.height = Mathf.Max(pos.height, DEFAULT_WINDOW_HEIGHT);
                window.position = pos;
            }
            window.Show();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            EditorApplication.update += OnEditorUpdate;
            EnsureAudioInitialized();
            LoadRecentPaths();
            RefreshAvailableMicrophones();

            if (SettingsManager.Settings != null)
            {
                _readAheadBufferMs = SettingsManager.Settings.PlaybackBufferLength.Value;
                _audioCalibrationMs = SettingsManager.Settings.AudioCalibration.Value;
                _micMonitoringVolume = (float) SettingsManager.Settings.VocalMonitoring.Value;
                _metronomeVolume = (float) SettingsManager.Settings.MetronomeVolume.Value;
                if (SettingsManager.Settings.MetronomeSound.Value != MetronomeSample.None)
                {
                    _testMetronomeSound = SettingsManager.Settings.MetronomeSound.Value;
                }
                _metronomeTargetChannel = SettingsManager.Settings.OutputChannelMetronome.Value;
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _metronomeLoopRunning = false;
            StopDriftTest();
            DisposeSong();
            DisconnectMicrophone();
        }

        private void OnDestroy()
        {
            _metronomeLoopRunning = false;
            StopDriftTest();
            DisposeSong();
            DisconnectMicrophone();
        }

        private static void EnsureAudioInitialized()
        {
            PathHelper.Init();
            StemSettings.ApplySettings = true;

            if (!SettingsManager.SettingContainer.IsInitialized)
            {
                SettingsManager.LoadStartupSettings();
                GlobalAudioHandler.Initialize<BassAudioManager>();
                SettingsManager.LoadSettings();
            }
            else
            {
                GlobalAudioHandler.Initialize<BassAudioManager>();
            }

            StemSettings.ApplySettings = true;
        }

        private void DisposeSong()
        {
            if (_bassSong != null)
            {
                _bassSong.SongEnd -= OnSongEnd;
                _bassSong.Dispose();
                _bassSong = null;
                _audioSynchronizer = null;
            }

            foreach (var stem in ALL_STEMS)
            {
                GlobalAudioHandler.SetReverbSetting(stem, false);
            }
            _stemReverbs.Clear();

            if (_smoothedFft != null && _peakFft != null)
            {
                Array.Clear(_smoothedFft, 0, _smoothedFft.Length);
                for (int i = 0; i < _peakFft.Length; i++)
                {
                    _peakFft[i] = _fftMinDb;
                }
            }
            for (int b = 0; b < _fftBands.Length; b++)
            {
                _fftBands[b].CurrentDb = _fftMinDb;
                _fftBands[b].PeakDb = _fftMinDb;
            }
            _dominantFrequencyHz = 0f;
            _dominantDb = _fftMinDb;
            _dominantNoteName = "--";
            _dominantCents = 0f;
            _spectralCentroidHz = 0f;
        }

        private void OnSongEnd()
        {
            if (_isDriftTestRunning && _bassSong != null)
            {
                _driftAudioLoopCount++;
                _bassSong.SetPosition(0);
                PlaySong();
            }
            Repaint();
        }

        private void OnEditorUpdate()
        {
            GlobalAudioHandler.Update();

            double now = EditorApplication.timeSinceStartup;
            double dt = now - _lastUpdateTime;
            _lastUpdateTime = now;

            UpdateMicrophone(now, dt);
            UpdateSongPlayback(now, dt);
            UpdateDriftTest(now, dt);
            UpdateFft(now, dt);
            UpdateMetronome(now, dt);
        }

        private void UpdateMicrophone(double now, double dt)
        {
            if (_activeMicDevice == null)
            {
                return;
            }

            _activeMicDevice.IsRecordingOutput = true;
            bool hadFrame = false;

            while (_activeMicDevice.DequeueOutputFrame(out var frame))
            {
                hadFrame = true;
                _micFramesReceived++;
                _micFpsFrameCount++;

                if (_lastMicFrameTime > 0)
                {
                    _micFrameIntervalMs = (now - _lastMicFrameTime) * 1000.0;
                }
                _lastMicFrameTime = now;

                if (frame.IsHit)
                {
                    _lastHitTime = now;
                    _totalHitCount++;
                }
                else
                {
                    if (frame.Pitch > 0)
                    {
                        _micCurrentPitchHz = frame.Pitch;
                        _micCurrentMidi = frame.PitchAsMidiNote;
                        _micCurrentDb = frame.Volume;
                        _micIsVoiced = true;

                        int roundedMidi = (int) MathF.Round(_micCurrentMidi);
                        int noteIndex = ((roundedMidi % 12) + 12) % 12;
                        int octave = (roundedMidi / 12) - 1;
                        _micCurrentNoteName = $"{NOTE_NAMES[noteIndex]}{octave}";
                        _micCurrentCents = (_micCurrentMidi - roundedMidi) * 100f;
                    }
                    else
                    {
                        _micCurrentDb = frame.Volume;
                        _micIsVoiced = false;
                    }
                }
            }

            if (now - _lastMicFrameTime > 0.15)
            {
                _micIsVoiced = false;
                _micCurrentDb = Mathf.Lerp(_micCurrentDb, -160f, (float) (dt * 8.0));
            }

            if (_micCurrentDb > _micPeakDb)
            {
                _micPeakDb = _micCurrentDb;
                _micPeakHoldDb = _micCurrentDb;
                _lastMicPeakHoldTime = now;
            }
            else
            {
                _micPeakDb = Mathf.Lerp(_micPeakDb, _micCurrentDb, (float) (dt * 5.0));
                if (now - _lastMicPeakHoldTime > 1.0)
                {
                    _micPeakHoldDb = Mathf.Lerp(_micPeakHoldDb, _micCurrentDb, (float) (dt * 2.0));
                }
            }

            if (now - _lastMicFpsTime >= 1.0)
            {
                _micFps = (float) (_micFpsFrameCount / (now - _lastMicFpsTime));
                _micFpsFrameCount = 0;
                _lastMicFpsTime = now;
            }

            if (!_freezeGraph && now - _lastMicSampleTime >= SAMPLE_INTERVAL)
            {
                if (now - _lastMicSampleTime > SAMPLE_INTERVAL * 4.0 || _lastMicSampleTime <= 0)
                {
                    _lastMicSampleTime = now - SAMPLE_INTERVAL;
                }
                _lastMicSampleTime += SAMPLE_INTERVAL;

                _micSamples.Add(new MicSample
                {
                    RealTime = now,
                    MidiNote = _micIsVoiced ? _micCurrentMidi : 0f,
                    VolumeDb = _micCurrentDb,
                    IsHit = (now - _lastHitTime) < 0.06,
                    IsVoiced = _micIsVoiced
                });

                if (_micSamples.Count > MAX_SAMPLES)
                {
                    _micSamples.RemoveAt(0);
                }
            }

            if (hadFrame || _selectedBottomTab == 3 || _graphMode == GraphMode.MicPitchAndHits)
            {
                Repaint();
            }
        }

        private void UpdateSongPlayback(double now, double dt)
        {
            if (_bassSong == null || _bassSong.IsPaused || _freezeGraph)
            {
                return;
            }

            _playbackClock += dt;

            if (_simulatedClockDriftPercent != 0f)
            {
                _inputTimeOffset -= dt * (_simulatedClockDriftPercent / 100.0);
            }

            double currentInputSystemTime = InputManager.CurrentInputTime;
            if (_inputTimeOffset <= 0.0001)
            {
                _inputTimeOffset = currentInputSystemTime - (_bassSong.GetPosition() / _playbackSpeed);
            }

            double currentInputTime = (currentInputSystemTime - _inputTimeOffset + _simulatedClockDisturbance) * _playbackSpeed;
            double controlTargetTime = currentInputTime;
            double audioCalibrationSeconds = _audioCalibrationMs / 1000.0;
            double heardTargetTime = controlTargetTime + (audioCalibrationSeconds * _playbackSpeed);

            if (_modelSongSync && _audioSynchronizer != null && !_isDriftTestRunning)
            {
                _audioSynchronizer.Synchronize(controlTargetTime, heardTargetTime, _playbackSpeed,
                    currentInputSystemTime);
            }

            if (now - _lastSampleTime >= SAMPLE_INTERVAL)
            {
                if (now - _lastSampleTime > SAMPLE_INTERVAL * 4.0 || _lastSampleTime <= 0)
                {
                    _lastSampleTime = now - SAMPLE_INTERVAL;
                }
                _lastSampleTime += SAMPLE_INTERVAL;

                _fpsFrameCount++;
                if (now - _lastFpsUpdateTime >= 1.0)
                {
                    _currentFps = (float) (_fpsFrameCount / (now - _lastFpsUpdateTime));
                    _fpsFrameCount = 0;
                    _lastFpsUpdateTime = now;
                }

                var syncPos = _bassSong.GetSyncPosition();
                double positionSampleTime = EditorApplication.timeSinceStartup;
                double positionSampleInputTime = InputManager.CurrentInputTime;
                double targetAdvance = (positionSampleInputTime - currentInputSystemTime) * _playbackSpeed;
                double sampledControlTargetTime = controlTargetTime + targetAdvance;
                double sampledHeardTargetTime = heardTargetTime + targetAdvance;
                var readAheadStats = _bassSong.GetReadAheadStats();
                _latestReadAheadStats = readAheadStats;
                int sampleRate = Bass.Info.SampleRate;
                double heardErrMs = (sampledHeardTargetTime - syncPos.Heard) * 1000.0;
                double ctrlErrMs = _audioSynchronizer != null && _modelSongSync
                    ? _audioSynchronizer.ControlError * 1000.0
                    : (sampledControlTargetTime - syncPos.Control) * 1000.0;

                float adjustment = _audioSynchronizer?.EffectiveAdjustment ?? 0f;
                var syncState = _audioSynchronizer?.State ?? AudioSynchronizer.SyncState.Idle;

                _samples.Add(new PositionSample
                {
                    RealTime = _playbackClock + positionSampleTime - now,
                    TargetTime = sampledControlTargetTime,
                    HeardPosition = syncPos.Heard,
                    ControlPosition = syncPos.Control,
                    OutputFramePosition = sampleRate > 0
                        ? readAheadStats.PositionOutputFrame / (double) sampleRate
                        : 0,
                    CallbackFramesMs = sampleRate > 0
                        ? readAheadStats.CallbackFrames * 1000.0 / sampleRate
                        : 0,
                    CallbackElapsedMs = sampleRate > 0
                        ? readAheadStats.CallbackElapsedFrames * 1000.0 / sampleRate
                        : 0,
                    CallbackCorrectionMs = sampleRate > 0
                        ? readAheadStats.CallbackCorrectionFrames * 1000.0 / sampleRate
                        : 0,
                    CallbackClockOffsetMs = sampleRate > 0
                        ? readAheadStats.CallbackClockOffsetFrames * 1000.0 / sampleRate
                        : 0,
                    HeardErrorMs = heardErrMs,
                    ControlErrorMs = ctrlErrMs,
                    DriftErrorMs = _driftCumulativeMs,
                    Adjustment = adjustment,
                    SyncState = syncState,
                    IsPlaying = true
                });

                if (_samples.Count > MAX_SAMPLES)
                {
                    _samples.RemoveAt(0);
                }

                Repaint();
            }
        }

        private void UpdateFft(double now, double dt)
        {
            int fftSize = 1 << _fftSizeLog;
            int binCount = fftSize / 2;

            if (_fftBuffer == null || _fftBuffer.Length != binCount)
            {
                _fftBuffer = new float[binCount];
                _smoothedFft = new float[binCount];
                _peakFft = new float[binCount];
                for (int i = 0; i < binCount; i++)
                {
                    _peakFft[i] = _fftMinDb;
                }
            }

            bool isPlaying = _bassSong != null && !_bassSong.IsPaused;

            if (isPlaying && !_freezeGraph)
            {
                int bytesRead = _bassSong!.GetFFTData(_fftBuffer, _fftSizeLog, false);
                _lastFftBytesRead = bytesRead;
                if (bytesRead > 0)
                {
                    int sampleRate = Bass.Info.SampleRate > 0 ? Bass.Info.SampleRate : 44100;
                    float nyquist = sampleRate * 0.5f;
                    float freqPerBin = nyquist / binCount;

                    float maxMag = 0f;
                    int maxBin = 0;
                    double weightedFreqSum = 0;
                    double totalMagSum = 0;

                    float smooth = Mathf.Clamp01(_fftSmoothingFactor);
                    float peakDecay = _fftPeakDecayRate * (float) dt;

                    for (int i = 0; i < binCount; i++)
                    {
                        float rawMag = _fftBuffer[i];
                        _smoothedFft![i] = (_smoothedFft[i] * smooth) + (rawMag * (1f - smooth));
                        float curMag = _smoothedFft[i];

                        float db = 20f * Mathf.Log10(Mathf.Max(curMag, 1e-6f));

                        if (db > _peakFft![i])
                        {
                            _peakFft[i] = db;
                        }
                        else
                        {
                            _peakFft[i] = Mathf.Max(_fftMinDb, _peakFft[i] - peakDecay);
                        }

                        if (curMag > maxMag)
                        {
                            maxMag = curMag;
                            maxBin = i;
                        }

                        float freq = i * freqPerBin;
                        weightedFreqSum += freq * curMag;
                        totalMagSum += curMag;
                    }

                    if (maxMag > 1e-4f)
                    {
                        _dominantFrequencyHz = maxBin * freqPerBin;
                        _dominantDb = 20f * Mathf.Log10(Mathf.Max(maxMag, 1e-6f));

                        if (_dominantFrequencyHz >= 20f)
                        {
                            float midi = FreqToMidi(_dominantFrequencyHz);
                            int roundedMidi = (int) MathF.Round(midi);
                            int noteIndex = ((roundedMidi % 12) + 12) % 12;
                            int octave = (roundedMidi / 12) - 1;
                            _dominantNoteName = $"{NOTE_NAMES[noteIndex]}{octave}";
                            _dominantCents = (midi - roundedMidi) * 100f;
                        }
                        else
                        {
                            _dominantNoteName = "--";
                            _dominantCents = 0f;
                        }
                    }
                    else
                    {
                        _dominantFrequencyHz = 0f;
                        _dominantDb = _fftMinDb;
                        _dominantNoteName = "--";
                        _dominantCents = 0f;
                    }

                    _spectralCentroidHz = totalMagSum > 1e-5 ? (float) (weightedFreqSum / totalMagSum) : 0f;

                    for (int b = 0; b < _fftBands.Length; b++)
                    {
                        float minF = _fftBands[b].MinFreq;
                        float maxF = _fftBands[b].MaxFreq;
                        int startBin = Math.Clamp((int) (minF / freqPerBin), 0, binCount - 1);
                        int endBin = Math.Clamp((int) (maxF / freqPerBin), startBin, binCount - 1);

                        float bandMax = 0f;
                        for (int i = startBin; i <= endBin; i++)
                        {
                            if (_smoothedFft![i] > bandMax)
                            {
                                bandMax = _smoothedFft[i];
                            }
                        }

                        float bandDb = 20f * Mathf.Log10(Mathf.Max(bandMax, 1e-6f));
                        _fftBands[b].CurrentDb = bandDb;
                        if (bandDb > _fftBands[b].PeakDb)
                        {
                            _fftBands[b].PeakDb = bandDb;
                        }
                        else
                        {
                            _fftBands[b].PeakDb = Mathf.Max(_fftMinDb, _fftBands[b].PeakDb - peakDecay);
                        }
                    }
                }
            }
            else if (!isPlaying && _smoothedFft != null && _peakFft != null)
            {
                float decay = (float) (dt * 15f);
                float peakDecay = _fftPeakDecayRate * (float) dt;
                for (int i = 0; i < _smoothedFft.Length; i++)
                {
                    _smoothedFft[i] = Mathf.Max(0f, _smoothedFft[i] - decay);
                    _peakFft[i] = Mathf.Max(_fftMinDb, _peakFft[i] - peakDecay);
                }
                for (int b = 0; b < _fftBands.Length; b++)
                {
                    _fftBands[b].CurrentDb = Mathf.Max(_fftMinDb, _fftBands[b].CurrentDb - peakDecay);
                    _fftBands[b].PeakDb = Mathf.Max(_fftMinDb, _fftBands[b].PeakDb - peakDecay);
                }
            }

            if (_graphMode == GraphMode.FrequencySpectrum || _graphMode == GraphMode.Oscilloscope)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            try
            {
                EnsureAudioInitialized();
            }
            catch (Exception ex)
            {
                EditorGUILayout.HelpBox($"Audio initialization status: {ex.Message}", MessageType.Warning);
            }

            HandleDragAndDrop();
            HandleKeyboardShortcuts();

            using (var scroll = new EditorGUILayout.ScrollViewScope(_mainScroll))
            {
                _mainScroll = scroll.scrollPosition;

                EditorGUILayout.Space(6);
                DrawTopBar();

                if (_showLibrarySection)
                {
                    EditorGUILayout.Space(6);
                    DrawLibraryDrawer();
                }

                EditorGUILayout.Space(6);
                DrawTransportBar();

                EditorGUILayout.Space(6);
                DrawOscilloscopeCard();

                EditorGUILayout.Space(6);
                DrawBottomDashboard();

                EditorGUILayout.Space(8);
            }

            if (Event.current.type == EventType.MouseMove)
            {
                Repaint();
            }
        }

        private void HandleDragAndDrop()
        {
            var evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
            {
                return;
            }

            if (DragAndDrop.paths == null || DragAndDrop.paths.Length == 0)
            {
                return;
            }

            string path = DragAndDrop.paths[0];
            bool isDirectory = Directory.Exists(path);
            bool isFile = File.Exists(path);

            if (!isDirectory && !isFile)
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                if (isDirectory)
                {
                    LoadSongFolder(path);
                }
                else
                {
                    LoadAudioFile(path);
                }
                evt.Use();
            }
        }

        private void HandleKeyboardShortcuts()
        {
            var evt = Event.current;
            if (evt.type != EventType.KeyDown)
            {
                return;
            }

            if (evt.keyCode == KeyCode.Space)
            {
                TogglePlayPause();
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.LeftArrow && evt.control)
            {
                JumpRelative(-5.0);
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.RightArrow && evt.control)
            {
                JumpRelative(5.0);
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.LeftArrow)
            {
                JumpRelative(-1.0);
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.RightArrow)
            {
                JumpRelative(1.0);
                evt.Use();
            }
        }

        private void PlaySong()
        {
            if (_bassSong == null || !_bassSong.IsPaused)
            {
                return;
            }

            _lastUpdateTime = EditorApplication.timeSinceStartup;
            _lastSampleTime = EditorApplication.timeSinceStartup;
            double currentPos = _bassSong.GetPosition();

            _bassSong.Pause();
            _bassSong.SetOutputLatency(_audioCalibrationMs / 1000.0);
            _audioSynchronizer?.Reset(_playbackSpeed);
            _bassSong.SetPosition(currentPos);

            _bassSong.Play();
            double inputNow = InputManager.CurrentInputTime;
            _inputTimeOffset = inputNow - ((currentPos - _simulatedClockDisturbance) / _playbackSpeed);
            Repaint();
        }

        private void PauseSong()
        {
            if (_bassSong == null || _bassSong.IsPaused)
            {
                return;
            }

            _bassSong.Pause();
            Repaint();
        }

        private void StopSong()
        {
            if (_bassSong == null)
            {
                return;
            }

            _bassSong.Pause();
            _bassSong.SetOutputLatency(_audioCalibrationMs / 1000.0);
            _audioSynchronizer?.Reset(_playbackSpeed);
            _bassSong.SetPosition(0);
            _playbackClock = 0;
            _simulatedClockDisturbance = 0;
            _simulatedClockDriftPercent = 0;
            _inputTimeOffset = InputManager.CurrentInputTime;
            _samples.Clear();
            _viewEndTime = -1;
            Repaint();
        }

        private void SeekSong(double targetPosition)
        {
            if (_bassSong == null)
            {
                return;
            }

            double totalLength = _bassSong.Length;
            double target = Math.Clamp(targetPosition, 0, totalLength);
            bool isPlaying = !_bassSong.IsPaused;

            _bassSong.Pause();
            _bassSong.SetOutputLatency(_audioCalibrationMs / 1000.0);
            _audioSynchronizer?.Reset(_playbackSpeed);
            _bassSong.SetPosition(target);
            _playbackClock = target;

            if (isPlaying)
            {
                _bassSong.Play();
            }

            double inputNow = InputManager.CurrentInputTime;
            _inputTimeOffset = inputNow - ((target - _simulatedClockDisturbance) / _playbackSpeed);
            Repaint();
        }

        private void TogglePlayPause()
        {
            if (_bassSong == null)
            {
                return;
            }

            if (_bassSong.IsPaused)
            {
                PlaySong();
            }
            else
            {
                PauseSong();
            }
        }

        private void JumpRelative(double deltaSeconds)
        {
            if (_bassSong == null)
            {
                return;
            }

            SeekSong(_bassSong.GetPosition() + deltaSeconds);
        }

        private void DrawTopBar()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isLoaded = _bassSong != null;
                    bool isPlaying = _bassSong?.IsPaused == false;

                    var prevBg = GUI.backgroundColor;
                    if (isLoaded && isPlaying)
                    {
                        GUI.backgroundColor = new Color(0.18f, 0.78f, 0.38f, 1f);
                        GUILayout.Label("● PLAYING", EditorStyles.miniButton, GUILayout.Width(82), GUILayout.Height(20));
                    }
                    else if (isLoaded)
                    {
                        GUI.backgroundColor = new Color(0.95f, 0.65f, 0.15f, 1f);
                        GUILayout.Label("⏸ PAUSED", EditorStyles.miniButton, GUILayout.Width(82), GUILayout.Height(20));
                    }
                    else
                    {
                        GUI.backgroundColor = new Color(0.40f, 0.45f, 0.52f, 1f);
                        GUILayout.Label("⏹ STOPPED", EditorStyles.miniButton, GUILayout.Width(82), GUILayout.Height(20));
                    }
                    GUI.backgroundColor = prevBg;

                    GUILayout.Space(6);

                    var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 12,
                        alignment = TextAnchor.MiddleLeft,
                        normal = { textColor = isLoaded ? Color.white : new Color(0.7f, 0.75f, 0.8f) }
                    };
                    EditorGUILayout.LabelField(new GUIContent(_loadedSongName, _sourcePath ?? _loadedSongName), titleStyle, GUILayout.Height(20));

                    GUILayout.FlexibleSpace();

                    string currentDevice = SettingsManager.Settings?.OutputDevice.Value ?? "Default";
                    var currentMode = GlobalAudioHandler.GetOutputMode(currentDevice);
                    string cleanDev = CleanDeviceName(currentDevice);
                    string devButtonLabel = currentMode == AudioOutputMode.Asio ? $"⚡ ASIO: {cleanDev} ▾" : $"🔊 {cleanDev} ▾";

                    if (GUILayout.Button(devButtonLabel, EditorStyles.miniButton, GUILayout.Height(20), GUILayout.MaxWidth(240)))
                    {
                        ShowDeviceMenu();
                    }

                    GUILayout.Space(4);

                    if (GUILayout.Button("Open Audio ▾", EditorStyles.miniButton, GUILayout.Height(20), GUILayout.Width(105)))
                    {
                        ShowAudioMenu();
                    }

                    GUI.enabled = !string.IsNullOrEmpty(_sourcePath);
                    if (GUILayout.Button("Reveal", EditorStyles.miniButton, GUILayout.Height(20), GUILayout.Width(60)))
                    {
                        EditorUtility.RevealInFinder(_sourcePath);
                    }
                    GUI.enabled = true;
                }

                EditorGUILayout.Space(2);

                using (new EditorGUILayout.HorizontalScope())
                {
                    int sampleRate = Bass.Info.SampleRate;
                    int speakers = Bass.Info.SpeakerCount;
                    string activeDevice = SettingsManager.Settings?.OutputDevice.Value ?? "Default";
                    string cleanActive = CleanDeviceName(activeDevice);
                    var mode = GlobalAudioHandler.GetOutputMode(activeDevice);
                    string modeLabel = mode == AudioOutputMode.Asio ? "ASIO" : "Shared";
                    double latencyMs = GlobalAudioHandler.PlaybackLatency;
                    var bufferInfo = GlobalAudioHandler.GetOutputBufferInfo();
                    string bufferStr = bufferInfo is { } bInfo && bInfo.PreferredLength > 0 ? $" • {bInfo.PreferredLength} spl" : string.Empty;

                    string specBadge = $"{sampleRate} Hz  •  {speakers} ch  •  {latencyMs:F1} ms latency{bufferStr}";
                    string shortPath;
                    if (string.IsNullOrEmpty(_sourcePath))
                    {
                        shortPath = "Drag & drop audio file or folder to load";
                    }
                    else
                    {
                        shortPath = Path.GetFileName(_sourcePath);
                        string? dir = Path.GetDirectoryName(_sourcePath);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            string dirName = Path.GetFileName(dir);
                            shortPath = $".../{dirName}/{shortPath}";
                        }
                    }

                    var metaStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = new Color(0.6f, 0.65f, 0.72f) }
                    };
                    var specStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                    {
                        normal = { textColor = new Color(0.75f, 0.82f, 0.92f) },
                        alignment = TextAnchor.MiddleRight
                    };

                    EditorGUILayout.LabelField(new GUIContent($"📁 {shortPath}", _sourcePath), metaStyle);
                    EditorGUILayout.LabelField(specBadge, specStyle, GUILayout.Width(310));
                }
            }
        }

        private static string CleanDeviceName(string? rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "Default";
            string name = rawName!.Trim();
            while (name.StartsWith("ASIO: ", StringComparison.OrdinalIgnoreCase) || name.StartsWith("ASIO:", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(name.IndexOf(':') + 1).Trim();
            }
            return name;
        }

        private void ShowAudioMenu()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Browse Audio File..."), false, () =>
            {
                string path = EditorUtility.OpenFilePanel("Select Audio File", "", "ogg,opus,mp3,wav,aiff,mogg,sng");
                if (!string.IsNullOrEmpty(path))
                {
                    LoadAudioFile(path);
                }
            });

            menu.AddItem(new GUIContent("Browse Song Folder..."), false, () =>
            {
                string path = EditorUtility.OpenFolderPanel("Select Song Folder", "", "");
                if (!string.IsNullOrEmpty(path))
                {
                    LoadSongFolder(path);
                }
            });

            menu.AddItem(new GUIContent("Song Library Drawer"), _showLibrarySection, () =>
            {
                _showLibrarySection = !_showLibrarySection;
            });

            if (_recentPaths.Count > 0)
            {
                menu.AddSeparator("");
                for (int i = 0; i < _recentPaths.Count; i++)
                {
                    string p = _recentPaths[i];
                    string name = Path.GetFileName(p);
                    if (string.IsNullOrEmpty(name))
                    {
                        name = p;
                    }

                    menu.AddItem(new GUIContent($"Recent/{i + 1}. {name}"), false, () =>
                    {
                        if (Directory.Exists(p))
                        {
                            LoadSongFolder(p);
                        }
                        else if (File.Exists(p))
                        {
                            LoadAudioFile(p);
                        }
                    });
                }

                menu.AddItem(new GUIContent("Recent/Clear History"), false, () =>
                {
                    _recentPaths.Clear();
                    EditorPrefs.DeleteKey(RECENT_PATHS_KEY);
                });
            }

            menu.ShowAsContext();
        }

        private void ShowDeviceMenu()
        {
            var menu = new GenericMenu();
            var allDevices = GlobalAudioHandler.GetAllOutputDevices();
            string currentDevice = SettingsManager.Settings?.OutputDevice.Value ?? "Default";

            var sharedDevices = allDevices.Where(d => GlobalAudioHandler.GetOutputMode(d.name) == AudioOutputMode.Shared).ToList();
            var asioDevices = allDevices.Where(d => GlobalAudioHandler.GetOutputMode(d.name) == AudioOutputMode.Asio).ToList();

            foreach (var device in sharedDevices)
            {
                string devName = device.name;
                bool isCurrent = devName == currentDevice;
                menu.AddItem(new GUIContent($"Shared (WASAPI\\/DirectSound)/{devName}"), isCurrent, () =>
                {
                    SwitchOutputDevice(devName);
                });
            }

            if (asioDevices.Count > 0)
            {
                foreach (var device in asioDevices)
                {
                    string devName = device.name;
                    string displayName = devName.StartsWith("ASIO: ", StringComparison.Ordinal)
                        ? devName.Substring(6)
                        : devName;
                    bool isCurrent = devName == currentDevice;
                    menu.AddItem(new GUIContent($"ASIO (Low Latency)/{displayName}"), isCurrent, () =>
                    {
                        SwitchOutputDevice(devName);
                    });
                }
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("ASIO (Low Latency)/No ASIO Drivers Found"));
            }

            menu.ShowAsContext();
        }

        private void SwitchOutputDevice(string deviceName)
        {
            bool success;
            if (SettingsManager.SettingContainer.IsInitialized)
            {
                SettingsManager.Settings.OutputDevice.Value = deviceName;
                string active = SettingsManager.Settings.OutputDevice.Value;
                success = active == deviceName;
            }
            else
            {
                success = GlobalAudioHandler.SetOutputDevice(deviceName);
            }

            string activeDevice = SettingsManager.Settings?.OutputDevice.Value ?? "Default";
            if (!success)
            {
                _deviceStatusMessage = $"Failed to switch to '{deviceName}'. Active device: {activeDevice}";
                _deviceStatusIsError = true;
            }
            else
            {
                _deviceStatusMessage = $"Active device: {activeDevice}";
                _deviceStatusIsError = false;
            }

            _lastDeviceStatusTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void OpenAsioControlPanel()
        {
            if (SettingsManager.SettingContainer.IsInitialized)
            {
                SettingsManager.Settings.OpenAsioControlPanel();
            }
            else
            {
                GlobalAudioHandler.OpenOutputControlPanel();
                GlobalAudioHandler.ReinitializeOutput();
            }

            Repaint();
        }

        private void RestartOutput()
        {
            bool restarted = GlobalAudioHandler.ReinitializeOutput();
            _deviceStatusMessage = restarted ? "Output driver reinitialized successfully." : "Failed to reinitialize output driver.";
            _deviceStatusIsError = !restarted;
            _lastDeviceStatusTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void DrawLibraryDrawer()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Song Library", EditorStyles.boldLabel, GUILayout.Width(100));
                    _librarySearch = EditorGUILayout.TextField(_librarySearch);

                    if (GUILayout.Button("Scan Songs", GUILayout.Width(90), GUILayout.Height(19)))
                    {
                        _ = SongContainer.RunRefresh(false);
                    }

                    if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22), GUILayout.Height(19)))
                    {
                        _showLibrarySection = false;
                    }
                }

                var songs = SongContainer.Songs;
                if (songs == null || songs.Length == 0)
                {
                    EditorGUILayout.HelpBox("No songs currently indexed in SongContainer. Scan your song folders in YARG or click 'Scan Songs'.", MessageType.Info);
                    return;
                }

                var filtered = string.IsNullOrEmpty(_librarySearch)
                    ? songs.Take(40)
                    : songs.Where(s => s.Name.Original.Contains(_librarySearch, StringComparison.OrdinalIgnoreCase) ||
                                       s.Artist.Original.Contains(_librarySearch, StringComparison.OrdinalIgnoreCase)).Take(40);

                using var scroll = new EditorGUILayout.ScrollViewScope(_libraryScroll, GUILayout.Height(120));
                _libraryScroll = scroll.scrollPosition;

                foreach (var song in filtered)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"{song.Artist.Original} - {song.Name.Original}", EditorStyles.label);
                        if (GUILayout.Button("Load", GUILayout.Width(60), GUILayout.Height(18)))
                        {
                            LoadSongEntry(song);
                        }
                    }
                }
            }
        }

        private void DrawTransportBar()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool isLoaded = _bassSong != null;
                bool isPlaying = _bassSong?.IsPaused == false;
                double currentPos = _bassSong?.GetPosition() ?? 0;
                double totalLength = _bassSong?.Length ?? 0;

                using (new EditorGUILayout.HorizontalScope())
                {
                    var prevBg = GUI.backgroundColor;

                    GUI.enabled = isLoaded;
                    if (isPlaying)
                    {
                        GUI.backgroundColor = new Color(0.95f, 0.65f, 0.15f, 1f);
                        if (GUILayout.Button("⏸ Pause", EditorStyles.miniButton, GUILayout.Width(78), GUILayout.Height(22)))
                        {
                            PauseSong();
                        }
                    }
                    else
                    {
                        GUI.backgroundColor = isLoaded ? new Color(0.2f, 0.78f, 0.35f, 1f) : prevBg;
                        if (GUILayout.Button("▶ Play", EditorStyles.miniButton, GUILayout.Width(78), GUILayout.Height(22)))
                        {
                            PlaySong();
                        }
                    }
                    GUI.backgroundColor = prevBg;

                    if (GUILayout.Button("⏹ Stop", EditorStyles.miniButton, GUILayout.Width(58), GUILayout.Height(22)))
                    {
                        StopSong();
                    }

                    GUILayout.Space(6);

                    if (GUILayout.Button("-5s", EditorStyles.miniButtonLeft, GUILayout.Width(38), GUILayout.Height(22))) JumpRelative(-5.0);
                    if (GUILayout.Button("-1s", EditorStyles.miniButtonMid, GUILayout.Width(38), GUILayout.Height(22))) JumpRelative(-1.0);
                    if (GUILayout.Button("+1s", EditorStyles.miniButtonMid, GUILayout.Width(38), GUILayout.Height(22))) JumpRelative(1.0);
                    if (GUILayout.Button("+5s", EditorStyles.miniButtonRight, GUILayout.Width(38), GUILayout.Height(22))) JumpRelative(5.0);

                    GUILayout.Space(6);

                    var timeStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 11,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = new Color(0.9f, 0.93f, 0.96f) }
                    };

                    EditorGUILayout.LabelField($"{FormatTime(currentPos)} / {FormatTime(totalLength)}", timeStyle, GUILayout.Width(130), GUILayout.Height(22));

                    GUILayout.Space(4);

                    GUI.enabled = isLoaded && totalLength > 0;
                    float displayPos = _isScrubbing ? _scrubTarget : (float) currentPos;
                    EditorGUI.BeginChangeCheck();
                    float newPos = GUILayout.HorizontalSlider(displayPos, 0f, Mathf.Max(0.1f, (float) totalLength), GUILayout.Height(22));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _isScrubbing = true;
                        _scrubTarget = newPos;
                    }

                    if (_isScrubbing && Event.current.type == EventType.MouseUp)
                    {
                        _isScrubbing = false;
                        SeekSong(_scrubTarget);
                    }

                    GUI.enabled = true;
                }

                EditorGUILayout.Space(2);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Speed", EditorStyles.miniBoldLabel, GUILayout.Width(40));
                    DrawSpeedPill(0.5f, EditorStyles.miniButtonLeft);
                    DrawSpeedPill(0.75f, EditorStyles.miniButtonMid);
                    DrawSpeedPill(1.0f, EditorStyles.miniButtonMid);
                    DrawSpeedPill(1.25f, EditorStyles.miniButtonMid);
                    DrawSpeedPill(1.5f, EditorStyles.miniButtonRight);

                    GUILayout.Space(4);
                    float newSpeed = EditorGUILayout.Slider(_playbackSpeed, 0.1f, 2.5f, GUILayout.Width(85));
                    if (Mathf.Abs(newSpeed - _playbackSpeed) > 0.001f)
                    {
                        SetPlaybackSpeed(newSpeed);
                    }

                    if (GUILayout.Button("1x", EditorStyles.miniButton, GUILayout.Width(32)))
                    {
                        SetPlaybackSpeed(1f);
                    }

                    GUILayout.FlexibleSpace();

                    EditorGUILayout.LabelField("🔊 Volume", EditorStyles.miniBoldLabel, GUILayout.Width(62));
                    float newVol = EditorGUILayout.Slider(_volume, 0f, 1f, GUILayout.Width(100));
                    if (Mathf.Abs(newVol - _volume) > 0.001f)
                    {
                        _volume = newVol;
                        _bassSong?.SetVolume(_volume);
                    }
                    EditorGUILayout.LabelField($"{(int)(_volume * 100)}%", EditorStyles.miniLabel, GUILayout.Width(32));
                }
            }
        }

        private void SetPlaybackSpeed(float speed)
        {
            _playbackSpeed = speed;
            if (_bassSong == null)
            {
                return;
            }

            double currentInputSystemTime = InputManager.CurrentInputTime;
            double currentPos = _bassSong.GetPosition();
            _inputTimeOffset = currentInputSystemTime - ((currentPos - _simulatedClockDisturbance) / _playbackSpeed);

            if (_audioSynchronizer != null && _modelSongSync)
            {
                _audioSynchronizer.ChangeSongSpeed(_playbackSpeed);
            }
            else
            {
                _bassSong.SetPlaybackSpeed(_playbackSpeed);
            }
        }

        private void DrawSpeedPill(float speed, GUIStyle? style = null)
        {
            style ??= EditorStyles.miniButton;
            bool isActive = Mathf.Approximately(_playbackSpeed, speed);
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.25f, 0.65f, 1f, 1f);
            }

            if (GUILayout.Button($"{speed:0.##}x", style, GUILayout.Width(46), GUILayout.Height(18)))
            {
                SetPlaybackSpeed(speed);
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawOscilloscopeCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Audio Visualizer", EditorStyles.boldLabel, GUILayout.Width(120));

                    GUILayout.FlexibleSpace();

                    int currentModeIdx = Array.FindIndex(GRAPH_MODE_ITEMS, i => i.Mode == _graphMode);
                    if (currentModeIdx < 0) currentModeIdx = 0;
                    int newModeIdx = EditorGUILayout.Popup(currentModeIdx, GRAPH_MODE_LABELS, GUILayout.Width(190));
                    if (newModeIdx >= 0 && newModeIdx < GRAPH_MODE_ITEMS.Length && newModeIdx != currentModeIdx)
                    {
                        _graphMode = GRAPH_MODE_ITEMS[newModeIdx].Mode;
                    }

                    if (_graphMode == GraphMode.FrequencySpectrum)
                    {
                        GUILayout.Space(6);
                        EditorGUILayout.LabelField("FFT:", EditorStyles.miniLabel, GUILayout.Width(26));
                        DrawFftSizePill(8, "256", EditorStyles.miniButtonLeft);
                        DrawFftSizePill(9, "512", EditorStyles.miniButtonMid);
                        DrawFftSizePill(10, "1k", EditorStyles.miniButtonMid);
                        DrawFftSizePill(11, "2k", EditorStyles.miniButtonMid);
                        DrawFftSizePill(12, "4k", EditorStyles.miniButtonRight);

                        GUILayout.Space(6);
                        DrawFftStylePill(FftDisplayStyle.FilledCurve, "Curve", EditorStyles.miniButtonLeft);
                        DrawFftStylePill(FftDisplayStyle.RtaBars, "Bars", EditorStyles.miniButtonMid);
                        DrawFftStylePill(FftDisplayStyle.Both, "Both", EditorStyles.miniButtonRight);

                        GUILayout.Space(6);
                        DrawFftScalePill(FftScaleMode.Logarithmic, "Log", EditorStyles.miniButtonLeft);
                        DrawFftScalePill(FftScaleMode.Linear, "Lin", EditorStyles.miniButtonRight);
                    }
                    else if (_graphMode == GraphMode.Oscilloscope)
                    {
                        GUILayout.Space(6);
                        EditorGUILayout.LabelField("Timebase:", EditorStyles.miniLabel, GUILayout.Width(54));
                        DrawScopeTimebasePill(0.002f, "2ms", EditorStyles.miniButtonLeft);
                        DrawScopeTimebasePill(0.005f, "5ms", EditorStyles.miniButtonMid);
                        DrawScopeTimebasePill(0.010f, "10ms", EditorStyles.miniButtonMid);
                        DrawScopeTimebasePill(0.020f, "20ms", EditorStyles.miniButtonMid);
                        DrawScopeTimebasePill(0.050f, "50ms", EditorStyles.miniButtonRight);

                        GUILayout.Space(6);
                        EditorGUILayout.LabelField("Gain:", EditorStyles.miniLabel, GUILayout.Width(32));
                        DrawScopeGainPill(1f, "1x", EditorStyles.miniButtonLeft);
                        DrawScopeGainPill(2f, "2x", EditorStyles.miniButtonMid);
                        DrawScopeGainPill(5f, "5x", EditorStyles.miniButtonRight);
                    }
                    else if (_graphMode == GraphMode.MicPitchAndHits)
                    {
                        GUILayout.Space(6);
                        EditorGUILayout.LabelField("Win:", EditorStyles.miniLabel, GUILayout.Width(26));
                        DrawWindowPill(1f, "1s", EditorStyles.miniButtonLeft);
                        DrawWindowPill(2f, "2s", EditorStyles.miniButtonMid);
                        DrawWindowPill(3f, "3s", EditorStyles.miniButtonMid);
                        DrawWindowPill(5f, "5s", EditorStyles.miniButtonMid);
                        DrawWindowPill(10f, "10s", EditorStyles.miniButtonRight);

                        GUILayout.Space(6);
                        EditorGUILayout.LabelField("Range: C2–C6", EditorStyles.miniLabel, GUILayout.Width(85));
                    }
                    else
                    {
                        GUILayout.Space(6);
                        EditorGUILayout.LabelField("Win:", EditorStyles.miniLabel, GUILayout.Width(26));
                        DrawWindowPill(1f, "1s", EditorStyles.miniButtonLeft);
                        DrawWindowPill(2f, "2s", EditorStyles.miniButtonMid);
                        DrawWindowPill(3f, "3s", EditorStyles.miniButtonMid);
                        DrawWindowPill(5f, "5s", EditorStyles.miniButtonMid);
                        DrawWindowPill(10f, "10s", EditorStyles.miniButtonRight);

                        if (_graphMode != GraphMode.AbsolutePosition)
                        {
                            GUILayout.Space(6);
                            EditorGUILayout.LabelField("Y:", EditorStyles.miniLabel, GUILayout.Width(16));
                            DrawYScalePill(0f, "Auto", EditorStyles.miniButtonLeft);
                            DrawYScalePill(5f, "±5", EditorStyles.miniButtonMid);
                            DrawYScalePill(10f, "±10", EditorStyles.miniButtonMid);
                            DrawYScalePill(25f, "±25", EditorStyles.miniButtonRight);
                        }
                    }

                    GUILayout.Space(6);

                    var prevBg = GUI.backgroundColor;
                    if (_autoScroll)
                    {
                        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f, 1f);
                    }
                    if (GUILayout.Button(_autoScroll ? "● Live" : "Live", EditorStyles.miniButton, GUILayout.Width(56)))
                    {
                        _autoScroll = !_autoScroll;
                        if (_autoScroll)
                        {
                            if (_graphMode == GraphMode.MicPitchAndHits && _micSamples.Count > 0)
                            {
                                _viewEndTime = _micSamples[_micSamples.Count - 1].RealTime;
                            }
                            else if (_samples.Count > 0)
                            {
                                _viewEndTime = _samples[_samples.Count - 1].RealTime;
                            }
                        }
                    }
                    GUI.backgroundColor = prevBg;

                    if (_freezeGraph)
                    {
                        GUI.backgroundColor = new Color(0.95f, 0.65f, 0.15f, 1f);
                    }
                    if (GUILayout.Button("Freeze", EditorStyles.miniButton, GUILayout.Width(56)))
                    {
                        _freezeGraph = !_freezeGraph;
                    }
                    GUI.backgroundColor = prevBg;

                    if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        _samples.Clear();
                        _micSamples.Clear();
                        _viewEndTime = -1;
                        if (_smoothedFft != null && _peakFft != null)
                        {
                            Array.Clear(_smoothedFft, 0, _smoothedFft.Length);
                            for (int i = 0; i < _peakFft.Length; i++)
                            {
                                _peakFft[i] = _fftMinDb;
                            }
                        }
                        for (int b = 0; b < _fftBands.Length; b++)
                        {
                            _fftBands[b].CurrentDb = _fftMinDb;
                            _fftBands[b].PeakDb = _fftMinDb;
                        }
                    }
                }

                EditorGUILayout.Space(4);

                Rect graphRect = GUILayoutUtility.GetRect(100, 2000, 160, 185);
                DrawGraphArea(graphRect);

                EditorGUILayout.Space(3);
                DrawGraphTimelineMiniBar();

                EditorGUILayout.Space(4);
                DrawGraphHudRibbon();
            }
        }

        private void DrawWindowPill(float windowSeconds, string label, GUIStyle? style = null)
        {
            style ??= EditorStyles.miniButton;
            bool isActive = Mathf.Approximately(_graphTimeWindow, windowSeconds);
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button(label, style, GUILayout.Width(34), GUILayout.Height(18)))
            {
                _graphTimeWindow = windowSeconds;
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawYScalePill(float scaleMs, string label, GUIStyle? style = null)
        {
            style ??= EditorStyles.miniButton;
            bool isActive = Mathf.Approximately(_jitterScaleMs, scaleMs);
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button(label, style, GUILayout.Width(44), GUILayout.Height(18)))
            {
                _jitterScaleMs = scaleMs;
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawFftSizePill(int logSize, string label, GUIStyle? style = null)
        {
            style ??= EditorStyles.miniButton;
            bool isActive = _fftSizeLog == logSize;
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button(label, style, GUILayout.Width(40), GUILayout.Height(18)))
            {
                _fftSizeLog = logSize;
                _fftBuffer = null;
                _smoothedFft = null;
                _peakFft = null;
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawFftStylePill(FftDisplayStyle style, string label, GUIStyle? btnStyle = null)
        {
            btnStyle ??= EditorStyles.miniButton;
            bool isActive = _fftDisplayStyle == style;
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button(label, btnStyle, GUILayout.Width(52), GUILayout.Height(18)))
            {
                _fftDisplayStyle = style;
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawFftScalePill(FftScaleMode mode, string label, GUIStyle? btnStyle = null)
        {
            btnStyle ??= EditorStyles.miniButton;
            bool isActive = _fftScaleMode == mode;
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button(label, btnStyle, GUILayout.Width(40), GUILayout.Height(18)))
            {
                _fftScaleMode = mode;
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawScopeTimebasePill(float timebase, string label, GUIStyle? style = null)
        {
            style ??= EditorStyles.miniButton;
            bool isSelected = Mathf.Approximately(_scopeTimebase, timebase);
            var prevBg = GUI.backgroundColor;
            if (isSelected)
            {
                GUI.backgroundColor = new Color(0.2f, 0.8f, 1f, 1f);
            }
            if (GUILayout.Button(label, style, GUILayout.Width(42), GUILayout.Height(18)))
            {
                _scopeTimebase = timebase;
            }
            GUI.backgroundColor = prevBg;
        }

        private void DrawScopeGainPill(float gain, string label, GUIStyle? style = null)
        {
            style ??= EditorStyles.miniButton;
            bool isSelected = Mathf.Approximately(_scopeGain, gain);
            var prevBg = GUI.backgroundColor;
            if (isSelected)
            {
                GUI.backgroundColor = new Color(0.2f, 0.8f, 1f, 1f);
            }
            if (GUILayout.Button(label, style, GUILayout.Width(34), GUILayout.Height(18)))
            {
                _scopeGain = gain;
            }
            GUI.backgroundColor = prevBg;
        }

        private static void BuildPositionJitterValues(IReadOnlyList<PositionSample> samples,
            List<double> heardValues, List<double>? controlValues = null)
        {
            heardValues.Add(0.0);
            controlValues?.Add(0.0);

            for (int i = 1; i < samples.Count; i++)
            {
                var previous = samples[i - 1];
                var current = samples[i];
                double elapsed = current.RealTime - previous.RealTime;
                if (elapsed <= 0)
                {
                    heardValues.Add(0.0);
                    controlValues?.Add(0.0);
                    continue;
                }

                heardValues.Add(((current.HeardPosition - previous.HeardPosition) - elapsed) * 1000.0);
                controlValues?.Add(((current.ControlPosition - previous.ControlPosition) - elapsed) * 1000.0);
            }
        }

        private void DrawGraphArea(Rect rect)
        {
            if (rect.width < 10 || rect.height < 10)
            {
                return;
            }

            float paddingLeft = 52f;
            float paddingBottom = 20f;
            float paddingTop = 10f;
            float paddingRight = 10f;

            float plotWidth = rect.width - paddingLeft - paddingRight;
            float plotHeight = rect.height - paddingTop - paddingBottom;
            var plotRect = new Rect(rect.x + paddingLeft, rect.y + paddingTop, plotWidth, plotHeight);

            if (_graphMode == GraphMode.Oscilloscope)
            {
                DrawMainOscilloscopeGraph(rect, plotRect, paddingLeft, paddingTop, paddingRight, paddingBottom, plotWidth, plotHeight);
                return;
            }

            if (_graphMode == GraphMode.MicPitchAndHits)
            {
                DrawMicGraph(rect, plotRect, paddingLeft, paddingTop, paddingRight, paddingBottom, plotWidth, plotHeight);
                return;
            }

            if (_graphMode == GraphMode.FrequencySpectrum)
            {
                DrawFftSpectrumGraph(rect, plotRect, paddingLeft, paddingTop, paddingRight, paddingBottom, plotWidth, plotHeight);
                return;
            }

            double latestTime = _samples.Count > 0 ? _samples[_samples.Count - 1].RealTime : 0;
            double firstTime = _samples.Count > 0 ? _samples[0].RealTime : 0;

            if (_autoScroll || _viewEndTime < 0)
            {
                _viewEndTime = latestTime;
            }
            else
            {
                _viewEndTime = Math.Clamp(_viewEndTime, firstTime + _graphTimeWindow, Math.Max(firstTime + _graphTimeWindow, latestTime));
            }

            double maxTime = _viewEndTime;
            double minTime = Math.Max(firstTime, maxTime - _graphTimeWindow);
            if (maxTime <= minTime)
            {
                maxTime = minTime + 1.0;
            }

            var evt = Event.current;
            if (evt.type == EventType.ScrollWheel && rect.Contains(evt.mousePosition))
            {
                float zoomFactor = 1f + (evt.delta.y * 0.08f);
                _graphTimeWindow = Mathf.Clamp(_graphTimeWindow * zoomFactor, 0.25f, 30f);
                evt.Use();
                Repaint();
            }
            else if (evt.type == EventType.MouseDrag && (evt.button == 0 || evt.button == 2) && plotRect.Contains(evt.mousePosition))
            {
                _autoScroll = false;
                double dt = (evt.delta.x / plotWidth) * (maxTime - minTime);
                _viewEndTime -= dt;
                _viewEndTime = Math.Clamp(_viewEndTime, firstTime + _graphTimeWindow, Math.Max(firstTime + _graphTimeWindow, latestTime));
                evt.Use();
                Repaint();
            }

            if (evt.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.10f, 1f));

            if (_samples.Count < 2)
            {
                var centeredStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 12
                };
                GUI.Label(rect, "No playback position sampled yet. Press Play to visualize real-time sliding window jitter.", centeredStyle);
                return;
            }

            var windowSamples = new List<PositionSample>();
            for (int i = 0; i < _samples.Count; i++)
            {
                var s = _samples[i];
                if (s.RealTime >= minTime && s.RealTime <= maxTime)
                {
                    windowSamples.Add(s);
                }
            }

            if (windowSamples.Count < 2)
            {
                return;
            }

            var heardValues = new List<double>(windowSamples.Count);
            var controlValues = new List<double>(windowSamples.Count);

            switch (_graphMode)
            {
                case GraphMode.SyncConvergence:
                {
                    for (int i = 0; i < windowSamples.Count; i++)
                    {
                        heardValues.Add(windowSamples[i].HeardErrorMs);
                        controlValues.Add(windowSamples[i].ControlErrorMs);
                    }
                    break;
                }

                case GraphMode.PositionJitter:
                {
                    BuildPositionJitterValues(windowSamples, heardValues, controlValues);
                    break;
                }

                case GraphMode.FrameStepDelta:
                {
                    heardValues.Add(16.67);
                    controlValues.Add(16.67);

                    for (int i = 1; i < windowSamples.Count; i++)
                    {
                        double dHeardMs = (windowSamples[i].HeardPosition - windowSamples[i - 1].HeardPosition) * 1000.0;
                        double dControlMs = (windowSamples[i].ControlPosition - windowSamples[i - 1].ControlPosition) * 1000.0;
                        heardValues.Add(dHeardMs);
                        controlValues.Add(dControlMs);
                    }
                    break;
                }

                case GraphMode.PositionMappingStep:
                {
                    heardValues.Add(16.67);
                    controlValues.Add(16.67);

                    for (int i = 1; i < windowSamples.Count; i++)
                    {
                        double heardStep = (windowSamples[i].HeardPosition - windowSamples[i - 1].HeardPosition) * 1000.0;
                        double outputStep = (windowSamples[i].OutputFramePosition - windowSamples[i - 1].OutputFramePosition) * 1000.0;
                        heardValues.Add(heardStep);
                        controlValues.Add(outputStep);
                    }
                    break;
                }

                case GraphMode.CallbackTimingStep:
                {
                    for (int i = 0; i < windowSamples.Count; i++)
                    {
                        heardValues.Add(windowSamples[i].CallbackFramesMs);
                        controlValues.Add(windowSamples[i].CallbackElapsedMs);
                    }
                    break;
                }

                case GraphMode.ControlHeardDelta:
                {
                    for (int i = 0; i < windowSamples.Count; i++)
                    {
                        double deltaMs = (windowSamples[i].ControlPosition - windowSamples[i].HeardPosition) * 1000.0;
                        heardValues.Add(0.0);
                        controlValues.Add(deltaMs);
                    }
                    break;
                }

                case GraphMode.ClockDrift:
                {
                    for (int i = 0; i < windowSamples.Count; i++)
                    {
                        heardValues.Add(0.0);
                        controlValues.Add(windowSamples[i].DriftErrorMs);
                    }
                    break;
                }

                case GraphMode.AbsolutePosition:
                default:
                {
                    for (int i = 0; i < windowSamples.Count; i++)
                    {
                        heardValues.Add(windowSamples[i].HeardPosition);
                        controlValues.Add(windowSamples[i].ControlPosition);
                    }
                    break;
                }
            }

            double minY = double.MaxValue;
            double maxY = double.MinValue;

            for (int i = 0; i < heardValues.Count; i++)
            {
                if (heardValues[i] < minY) minY = heardValues[i];
                if (heardValues[i] > maxY) maxY = heardValues[i];
                if (controlValues[i] < minY) minY = controlValues[i];
                if (controlValues[i] > maxY) maxY = controlValues[i];
            }

            if (_graphMode == GraphMode.SyncConvergence)
            {
                if (_jitterScaleMs > 0)
                {
                    minY = -_jitterScaleMs;
                    maxY = _jitterScaleMs;
                }
                else
                {
                    double absMax = Math.Max(Math.Abs(minY), Math.Abs(maxY));
                    absMax = Math.Max(absMax, 5.0);
                    minY = -absMax;
                    maxY = absMax;
                }
            }
            else if (_graphMode == GraphMode.PositionJitter)
            {
                if (_jitterScaleMs > 0)
                {
                    minY = -_jitterScaleMs;
                    maxY = _jitterScaleMs;
                }
                else
                {
                    double absMax = Math.Max(Math.Abs(minY), Math.Abs(maxY));
                    absMax = Math.Max(absMax, 1.0);
                    minY = -absMax;
                    maxY = absMax;
                }
            }
            else if (_graphMode == GraphMode.FrameStepDelta || _graphMode == GraphMode.PositionMappingStep)
            {
                if (_jitterScaleMs > 0)
                {
                    minY = Math.Max(0, 16.67 - _jitterScaleMs);
                    maxY = 16.67 + _jitterScaleMs;
                }
                else
                {
                    minY = Math.Min(minY, 0);
                    maxY = Math.Max(maxY, 33.33);
                }
            }
            else if (_graphMode == GraphMode.CallbackTimingStep)
            {
                minY = Math.Min(minY, 0);
                maxY = Math.Max(maxY, 25);
            }
            else if (_graphMode == GraphMode.ControlHeardDelta || _graphMode == GraphMode.ClockDrift)
            {
                if (_jitterScaleMs > 0)
                {
                    minY = -_jitterScaleMs;
                    maxY = _jitterScaleMs;
                }
                else
                {
                    double absMax = Math.Max(Math.Abs(minY), Math.Abs(maxY));
                    absMax = Math.Max(absMax, 5.0);
                    minY = -absMax;
                    maxY = absMax;
                }
            }

            if (minY >= maxY)
            {
                minY = -1.0;
                maxY = 1.0;
            }

            double yRange = maxY - minY;

            DrawGrid(rect, minTime, maxTime, minY, maxY, _graphMode);

            if (_graphMode == GraphMode.PositionJitter || _graphMode == GraphMode.ControlHeardDelta || _graphMode == GraphMode.SyncConvergence || _graphMode == GraphMode.ClockDrift)
            {
                float normZeroY = (float) ((0.0 - minY) / yRange);
                if (normZeroY >= 0f && normZeroY <= 1f)
                {
                    float screenZeroY = rect.y + paddingTop + plotHeight - (normZeroY * plotHeight);
                    EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, screenZeroY, plotWidth, 1), new Color(1f, 1f, 1f, 0.35f));
                }

                if (_graphMode == GraphMode.SyncConvergence)
                {
                    float normStartPos = (float) ((3.0 - minY) / yRange);
                    float normStartNeg = (float) ((-3.0 - minY) / yRange);
                    if (normStartPos >= 0f && normStartPos <= 1f)
                    {
                        float screenY = rect.y + paddingTop + plotHeight - (normStartPos * plotHeight);
                        EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, screenY, plotWidth, 1), new Color(1f, 0.75f, 0.15f, 0.25f));
                    }
                    if (normStartNeg >= 0f && normStartNeg <= 1f)
                    {
                        float screenY = rect.y + paddingTop + plotHeight - (normStartNeg * plotHeight);
                        EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, screenY, plotWidth, 1), new Color(1f, 0.75f, 0.15f, 0.25f));
                    }

                    float normStopPos = (float) ((1.5 - minY) / yRange);
                    float normStopNeg = (float) ((-1.5 - minY) / yRange);
                    if (normStopPos >= 0f && normStopPos <= 1f)
                    {
                        float screenY = rect.y + paddingTop + plotHeight - (normStopPos * plotHeight);
                        EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, screenY, plotWidth, 1), new Color(0.2f, 0.85f, 0.35f, 0.20f));
                    }
                    if (normStopNeg >= 0f && normStopNeg <= 1f)
                    {
                        float screenY = rect.y + paddingTop + plotHeight - (normStopNeg * plotHeight);
                        EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, screenY, plotWidth, 1), new Color(0.2f, 0.85f, 0.35f, 0.20f));
                    }
                }
            }
            else if (_graphMode == GraphMode.FrameStepDelta || _graphMode == GraphMode.PositionMappingStep)
            {
                float normNominalY = (float) ((16.67 - minY) / yRange);
                if (normNominalY >= 0f && normNominalY <= 1f)
                {
                    float screenNominalY = rect.y + paddingTop + plotHeight - (normNominalY * plotHeight);
                    EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, screenNominalY, plotWidth, 1), new Color(1f, 1f, 1f, 0.3f));
                }
            }

            if (_graphMode == GraphMode.SyncConvergence)
            {
                int i = 0;
                while (i < windowSamples.Count)
                {
                    var s = windowSamples[i];
                    bool isSettling = s.SyncState == AudioSynchronizer.SyncState.Settling;
                    bool isAdjusting = Math.Abs(s.Adjustment) > 0.0001f || s.SyncState == AudioSynchronizer.SyncState.Correcting;

                    if (!isSettling && !isAdjusting)
                    {
                        i++;
                        continue;
                    }

                    int startIdx = i;
                    bool currentIsSettling = isSettling;

                    while (i < windowSamples.Count)
                    {
                        var next = windowSamples[i];
                        bool nextSettling = next.SyncState == AudioSynchronizer.SyncState.Settling;
                        bool nextAdjusting = Math.Abs(next.Adjustment) > 0.0001f || next.SyncState == AudioSynchronizer.SyncState.Correcting;

                        if (currentIsSettling ? !nextSettling : !nextAdjusting)
                        {
                            break;
                        }

                        i++;
                    }

                    double startTime = windowSamples[startIdx].RealTime;
                    double endTime = i < windowSamples.Count ? windowSamples[i].RealTime : windowSamples[i - 1].RealTime + SAMPLE_INTERVAL;

                    float normX0 = (float) ((startTime - minTime) / (maxTime - minTime));
                    float normX1 = (float) ((endTime - minTime) / (maxTime - minTime));
                    float x0 = rect.x + paddingLeft + (normX0 * plotWidth);
                    float x1 = rect.x + paddingLeft + (normX1 * plotWidth);
                    float width = Math.Max(1f, x1 - x0);

                    Color bandColor = currentIsSettling
                        ? new Color(0.25f, 0.65f, 1f, 0.08f)
                        : new Color(1f, 0.65f, 0.15f, 0.15f);

                    EditorGUI.DrawRect(new Rect(x0, rect.y + paddingTop, width, plotHeight), bandColor);
                }
            }

            var heardPoints = new List<Vector3>(windowSamples.Count);
            var controlPoints = new List<Vector3>(windowSamples.Count);
            var targetPoints = new List<Vector3>(windowSamples.Count);

            for (int i = 0; i < windowSamples.Count; i++)
            {
                float normX = (float) ((windowSamples[i].RealTime - minTime) / (maxTime - minTime));
                float screenX = rect.x + paddingLeft + (normX * plotWidth);

                float normHeardY = Mathf.Clamp01((float) ((heardValues[i] - minY) / yRange));
                float screenHeardY = rect.y + paddingTop + plotHeight - (normHeardY * plotHeight);
                heardPoints.Add(new Vector3(screenX, screenHeardY, 0));

                float normControlY = Mathf.Clamp01((float) ((controlValues[i] - minY) / yRange));
                float screenControlY = rect.y + paddingTop + plotHeight - (normControlY * plotHeight);
                controlPoints.Add(new Vector3(screenX, screenControlY, 0));

                if (_graphMode == GraphMode.AbsolutePosition)
                {
                    float normTargetY = Mathf.Clamp01((float) ((windowSamples[i].TargetTime - minY) / yRange));
                    float screenTargetY = rect.y + paddingTop + plotHeight - (normTargetY * plotHeight);
                    targetPoints.Add(new Vector3(screenX, screenTargetY, 0));
                }
            }

            Handles.color = new Color(0.25f, 0.28f, 0.35f, 1f);
            Handles.DrawPolyLine(
                new Vector3(rect.x + paddingLeft, rect.y + paddingTop, 0),
                new Vector3(rect.x + paddingLeft, rect.y + paddingTop + plotHeight, 0),
                new Vector3(rect.x + paddingLeft + plotWidth, rect.y + paddingTop + plotHeight, 0)
            );

            if (_graphMode == GraphMode.AbsolutePosition && targetPoints.Count > 1)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.75f);
                Handles.DrawAAPolyLine(1.8f, targetPoints.ToArray());
            }

            if (controlPoints.Count > 1)
            {
                Handles.color = new Color(1f, 0.65f, 0.15f, 0.9f);
                Handles.DrawAAPolyLine(2.2f, controlPoints.ToArray());
            }

            if (heardPoints.Count > 1)
            {
                Handles.color = new Color(0f, 0.85f, 1f, 1f);
                Handles.DrawAAPolyLine(2.2f, heardPoints.ToArray());
            }

            DrawGraphHoverCrosshair(rect, minTime, maxTime, paddingLeft, paddingTop, plotWidth, plotHeight, windowSamples, heardValues, controlValues);
        }

        private void DrawGraphHoverCrosshair(Rect rect, double minTime, double maxTime, float paddingLeft, float paddingTop, float plotWidth, float plotHeight, List<PositionSample> windowSamples, List<double> heardValues, List<double> controlValues)
        {
            Vector2 mousePos = Event.current.mousePosition;
            if (!rect.Contains(mousePos))
            {
                return;
            }

            float plotX = mousePos.x - (rect.x + paddingLeft);
            if (plotX < 0 || plotX > plotWidth || windowSamples.Count < 2)
            {
                return;
            }

            float normX = plotX / plotWidth;
            double targetTime = minTime + (normX * (maxTime - minTime));

            int closestIdx = 0;
            double minDiff = double.MaxValue;
            for (int i = 0; i < windowSamples.Count; i++)
            {
                double diff = Math.Abs(windowSamples[i].RealTime - targetTime);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closestIdx = i;
                }
            }

            var sample = windowSamples[closestIdx];
            double heardVal = heardValues[closestIdx];
            double controlVal = controlValues[closestIdx];

            float sampleNormX = (float) ((sample.RealTime - minTime) / (maxTime - minTime));
            float sampleScreenX = rect.x + paddingLeft + (sampleNormX * plotWidth);

            Handles.color = new Color(1f, 1f, 1f, 0.4f);
            Handles.DrawLine(
                new Vector3(sampleScreenX, rect.y + paddingTop, 0),
                new Vector3(sampleScreenX, rect.y + paddingTop + plotHeight, 0)
            );

            string tooltip = _graphMode switch
            {
                GraphMode.PositionJitter => $"Time: {sample.RealTime:F2}s\nHeard Jitter: {heardVal:+0.00;-0.00;0.00} ms\nCtrl Jitter: {controlVal:+0.00;-0.00;0.00} ms",
                GraphMode.SyncConvergence => $"Time: {sample.RealTime:F2}s\nHeard Err: {sample.HeardErrorMs:+0.00;-0.00;0.00} ms\nCtrl Err: {sample.ControlErrorMs:+0.00;-0.00;0.00} ms\nState: {sample.SyncState} ({sample.Adjustment * 100:+0.00;-0.00;0.00}%)",
                GraphMode.FrameStepDelta => $"Time: {sample.RealTime:F2}s\nHeard Step: {heardVal:F2} ms\nCtrl Step: {controlVal:F2} ms",
                GraphMode.PositionMappingStep => $"Time: {sample.RealTime:F2}s\nHeard Step: {heardVal:F2} ms\nOutput Step: {controlVal:F2} ms",
                GraphMode.CallbackTimingStep => $"Time: {sample.RealTime:F2}s\nCallback: {heardVal:F2} ms\nElapsed: {controlVal:F2} ms\nCorrection: {sample.CallbackCorrectionMs:+0.00;-0.00;0.00} ms\nClock Offset: {sample.CallbackClockOffsetMs:+0.00;-0.00;0.00} ms",
                GraphMode.ControlHeardDelta => $"Time: {sample.RealTime:F2}s\nDelta: {controlVal:+0.00;-0.00;0.00} ms",
                GraphMode.ClockDrift => $"Time: {sample.RealTime:F2}s\nDrift: {sample.DriftErrorMs:+0.00;-0.00;0.00} ms\nRate: {_driftRatePpm:+0.0;-0.0;0.0} ppm ({_driftMsPerMin:+0.00;-0.00;0.00} ms/min)",
                _ => $"Time: {sample.RealTime:F2}s\nTarget: {sample.TargetTime:F3}s\nHeard: {sample.HeardPosition:F3}s\nCtrl: {sample.ControlPosition:F3}s"
            };

            var tooltipContent = new GUIContent(tooltip);
            var tooltipStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };

            Vector2 tooltipSize = tooltipStyle.CalcSize(tooltipContent) + new Vector2(10, 6);
            float tooltipX = sampleScreenX + 12;
            if (tooltipX + tooltipSize.x > rect.x + rect.width - 6)
            {
                tooltipX = sampleScreenX - tooltipSize.x - 12;
            }

            float tooltipY = Mathf.Clamp(mousePos.y - (tooltipSize.y / 2), rect.y + paddingTop, rect.y + paddingTop + plotHeight - tooltipSize.y);
            GUI.Box(new Rect(tooltipX, tooltipY, tooltipSize.x, tooltipSize.y), tooltipContent, tooltipStyle);
        }

        private void DrawGraphTimelineMiniBar()
        {
            if (_graphMode == GraphMode.FrequencySpectrum || _graphMode == GraphMode.Oscilloscope)
            {
                if (_graphMode == GraphMode.FrequencySpectrum)
                {
                    DrawFftTimelineMiniBar();
                }
                return;
            }

            double firstTime;
            double latestTime;

            if (_graphMode == GraphMode.MicPitchAndHits)
            {
                if (_micSamples.Count < 2)
                {
                    return;
                }
                firstTime = _micSamples[0].RealTime;
                latestTime = _micSamples[_micSamples.Count - 1].RealTime;
            }
            else
            {
                if (_samples.Count < 2)
                {
                    return;
                }
                firstTime = _samples[0].RealTime;
                latestTime = _samples[_samples.Count - 1].RealTime;
            }

            double totalSpan = latestTime - firstTime;

            if (totalSpan <= 0.05)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                Rect barRect = GUILayoutUtility.GetRect(100, 1000, 14, 14);
                EditorGUI.DrawRect(barRect, new Color(0.12f, 0.13f, 0.16f, 1f));

                double maxTime = _autoScroll ? latestTime : _viewEndTime;
                double minTime = Math.Max(firstTime, maxTime - _graphTimeWindow);

                float normStart = (float) Math.Clamp((minTime - firstTime) / totalSpan, 0.0, 1.0);
                float normEnd = (float) Math.Clamp((maxTime - firstTime) / totalSpan, 0.0, 1.0);

                float viewX = barRect.x + (normStart * barRect.width);
                float viewWidth = Math.Max(6f, (normEnd - normStart) * barRect.width);

                EditorGUI.DrawRect(new Rect(viewX, barRect.y + 1, viewWidth, barRect.height - 2), new Color(0.2f, 0.6f, 0.95f, 0.45f));

                var evt = Event.current;
                if ((evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag) && barRect.Contains(evt.mousePosition))
                {
                    float clickedNorm = (evt.mousePosition.x - barRect.x) / barRect.width;
                    double clickedTime = firstTime + (clickedNorm * totalSpan);
                    _viewEndTime = Math.Clamp(clickedTime + (_graphTimeWindow * 0.5), firstTime + _graphTimeWindow, latestTime);
                    _autoScroll = false;
                    evt.Use();
                    Repaint();
                }

                if (!_autoScroll)
                {
                    GUILayout.Space(6);
                    if (GUILayout.Button("Jump to Live ⏩", EditorStyles.miniButton, GUILayout.Width(110), GUILayout.Height(14)))
                    {
                        _autoScroll = true;
                        _viewEndTime = latestTime;
                        Repaint();
                    }
                }
            }
        }

        private static void DrawGrid(Rect rect, double minTime, double maxTime, double minY, double maxY, GraphMode mode)
        {
            float paddingLeft = 52f;
            float paddingBottom = 20f;
            float paddingTop = 10f;
            float paddingRight = 10f;

            float plotWidth = rect.width - paddingLeft - paddingRight;
            float plotHeight = rect.height - paddingTop - paddingBottom;

            const int NUM_H_DIVS = 4;
            for (int i = 0; i <= NUM_H_DIVS; i++)
            {
                float normY = (float) i / NUM_H_DIVS;
                float y = rect.y + paddingTop + plotHeight - (normY * plotHeight);
                double yValue = minY + (normY * (maxY - minY));

                EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, y, plotWidth, 1), new Color(0.18f, 0.20f, 0.24f, 0.6f));

                string label = mode switch
                {
                    GraphMode.AbsolutePosition => $"{yValue:F2}s",
                    GraphMode.PositionJitter => $"{yValue:+0.0;-0.0;0.0}ms",
                    GraphMode.SyncConvergence => $"{yValue:+0.0;-0.0;0.0}ms",
                    GraphMode.FrameStepDelta => $"{yValue:F1}ms",
                    GraphMode.PositionMappingStep => $"{yValue:F1}ms",
                    GraphMode.ControlHeardDelta => $"{yValue:+0.0;-0.0;0.0}ms",
                    GraphMode.ClockDrift => $"{yValue:+0.0;-0.0;0.0}ms",
                    _ => $"{yValue:F1}"
                };

                GUI.Label(new Rect(rect.x, y - 9, paddingLeft - 4, 18), label, EditorStyles.miniLabel);
            }

            const int NUM_V_DIVS = 5;
            for (int i = 0; i <= NUM_V_DIVS; i++)
            {
                float normX = (float) i / NUM_V_DIVS;
                float x = rect.x + paddingLeft + (normX * plotWidth);
                double timeValue = minTime + (normX * (maxTime - minTime));

                EditorGUI.DrawRect(new Rect(x, rect.y + paddingTop, 1, plotHeight), new Color(0.18f, 0.20f, 0.24f, 0.6f));
                GUI.Label(new Rect(x - 25, rect.y + paddingTop + plotHeight + 2, 50, 16), $"{timeValue:F1}s", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawGraphHudRibbon()
        {
            if (_graphMode == GraphMode.MicPitchAndHits)
            {
                DrawMicHudRibbon();
                return;
            }

            if (_graphMode == GraphMode.FrequencySpectrum || _graphMode == GraphMode.Oscilloscope)
            {
                if (_graphMode == GraphMode.FrequencySpectrum)
                {
                    DrawFftHudRibbon();
                }
                return;
            }

            double heard = _bassSong?.GetPosition() ?? 0;
            double control = _bassSong?.GetControlPosition() ?? 0;
            double deltaMs = (control - heard) * 1000.0;

            double peakToPeakJitter = 0.0;
            double stdDevJitter = 0.0;

            if (_samples.Count >= 10)
            {
                double latest = _samples[_samples.Count - 1].RealTime;
                double windowStart = latest - _graphTimeWindow;
                var window = _samples.Where(s => s.RealTime >= windowStart).ToList();

                if (window.Count >= 4)
                {
                    var heardJitterValues = new List<double>(window.Count);
                    BuildPositionJitterValues(window, heardJitterValues);
                    var residuals = heardJitterValues.Skip(1).ToList();
                    peakToPeakJitter = residuals.Max() - residuals.Min();
                    double meanRes = residuals.Average();
                    stdDevJitter = Math.Sqrt(residuals.Average(r => (r - meanRes) * (r - meanRes)));
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (_graphMode == GraphMode.ClockDrift)
                {
                    double latestDrift = _samples.Count > 0 ? _samples[_samples.Count - 1].DriftErrorMs : _driftCumulativeMs;
                    Color driftColor = Math.Abs(latestDrift) > 10.0 ? new Color(1f, 0.35f, 0.35f) : (Math.Abs(latestDrift) > 3.0 ? new Color(1f, 0.75f, 0.2f) : new Color(0.25f, 0.95f, 0.45f));

                    DrawMetricTile("CUMULATIVE DRIFT", $"{latestDrift:+0.00;-0.00;0.00} ms", driftColor);
                    DrawMetricTile("ESTIMATED RATE", $"{_driftRatePpm:+0.0;-0.0;0.0} ppm", new Color(0.3f, 0.8f, 1f));
                    DrawMetricTile("DRIFT SPEED", $"{_driftMsPerMin:+0.00;-0.00;0.00} ms/min", new Color(0.85f, 0.65f, 1f));
                    DrawMetricTile("FRAME RATE", $"{_currentFps:F0} FPS", Color.white);
                }
                else if (_graphMode == GraphMode.SyncConvergence)
                {
                    double latestHeardErr = _samples.Count > 0 ? _samples[_samples.Count - 1].HeardErrorMs : 0;
                    double latestCtrlErr = _samples.Count > 0 ? _samples[_samples.Count - 1].ControlErrorMs : 0;
                    var state = _audioSynchronizer?.State ?? AudioSynchronizer.SyncState.Idle;
                    float adj = _audioSynchronizer?.EffectiveAdjustment ?? 0f;

                    var stateColor = !_modelSongSync ? Color.gray : (state == AudioSynchronizer.SyncState.Correcting ? new Color(1f, 0.7f, 0.15f) : (state == AudioSynchronizer.SyncState.Settling ? new Color(0.3f, 0.75f, 1f) : new Color(0.25f, 0.85f, 0.35f)));
                    string stateText = !_modelSongSync ? "SYNC OFF" : (state == AudioSynchronizer.SyncState.Correcting ? $"CORRECTING ({adj * 100:+0.0;-0.0}%)" : state.ToString().ToUpperInvariant());

                    DrawMetricTile("HEARD ERROR", $"{latestHeardErr:+0.00;-0.00;0.00} ms", new Color(0f, 0.85f, 1f));
                    DrawMetricTile("CONTROL ERROR", $"{latestCtrlErr:+0.00;-0.00;0.00} ms", new Color(1f, 0.65f, 0.15f));
                    DrawMetricTile("SYNC STATE", stateText, stateColor);
                    DrawMetricTile("FRAME RATE", $"{_currentFps:F0} FPS", Color.white);
                }
                else
                {
                    var jitterColor = peakToPeakJitter < 2.0 ? new Color(0.25f, 0.85f, 0.35f) : (peakToPeakJitter < 5.0 ? new Color(0.95f, 0.75f, 0.2f) : new Color(1f, 0.35f, 0.35f));

                    DrawMetricTile("PEAK-TO-PEAK", $"{peakToPeakJitter:F2} ms", jitterColor);
                    DrawMetricTile("STD DEVIATION", $"{stdDevJitter:F2} ms", new Color(0.8f, 0.85f, 0.9f));
                    DrawMetricTile("CTRL-HEARD Δ", $"{deltaMs:+0.0;-0.0;0.0} ms", new Color(1f, 0.65f, 0.15f));
                    DrawMetricTile("FRAME RATE", $"{_currentFps:F0} FPS", Color.white);
                }
            }
        }

        private void DrawMicGraph(Rect rect, Rect plotRect, float paddingLeft, float paddingTop, float paddingRight, float paddingBottom, float plotWidth, float plotHeight)
        {
            double now = EditorApplication.timeSinceStartup;
            double latestTime = _micSamples.Count > 0 ? _micSamples[_micSamples.Count - 1].RealTime : now;
            double firstTime = _micSamples.Count > 0 ? _micSamples[0].RealTime : now;

            if (_autoScroll || _viewEndTime < 0)
            {
                _viewEndTime = latestTime;
            }
            else
            {
                _viewEndTime = Math.Clamp(_viewEndTime, firstTime + _graphTimeWindow, Math.Max(firstTime + _graphTimeWindow, latestTime));
            }

            double maxTime = _viewEndTime;
            double minTime = maxTime - _graphTimeWindow;
            if (maxTime <= minTime)
            {
                maxTime = minTime + 1.0;
            }

            var evt = Event.current;
            if (evt.type == EventType.ScrollWheel && rect.Contains(evt.mousePosition))
            {
                float zoomFactor = 1f + (evt.delta.y * 0.08f);
                _graphTimeWindow = Mathf.Clamp(_graphTimeWindow * zoomFactor, 0.25f, 30f);
                evt.Use();
                Repaint();
            }
            else if (evt.type == EventType.MouseDrag && (evt.button == 0 || evt.button == 2) && plotRect.Contains(evt.mousePosition))
            {
                _autoScroll = false;
                double dt = (evt.delta.x / plotWidth) * (maxTime - minTime);
                _viewEndTime -= dt;
                _viewEndTime = Math.Clamp(_viewEndTime, firstTime + _graphTimeWindow, Math.Max(firstTime + _graphTimeWindow, latestTime));
                evt.Use();
                Repaint();
            }

            if (evt.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(rect, new Color(0.06f, 0.06f, 0.08f, 1f));

            if (_micSamples.Count < 2)
            {
                var centeredStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 12
                };
                GUI.Label(rect, "No microphone samples recorded yet. Connect an input below and sing or speak.", centeredStyle);
                return;
            }

            var windowSamples = new List<MicSample>();
            for (int i = 0; i < _micSamples.Count; i++)
            {
                var s = _micSamples[i];
                if (s.RealTime >= minTime && s.RealTime <= maxTime)
                {
                    windowSamples.Add(s);
                }
            }

            double minY = 36.0;
            double maxY = 84.0;
            double yRange = maxY - minY;

            int[] octaves = { 36, 48, 60, 72 };
            for (int i = 0; i < octaves.Length; i++)
            {
                float yBottom = (float) ((octaves[i] - minY) / yRange);
                float yTop = (float) (((octaves[i] + 12) - minY) / yRange);
                float screenY0 = rect.y + paddingTop + plotHeight - (yTop * plotHeight);
                float h = (yTop - yBottom) * plotHeight;
                Color laneBg = (i % 2 == 0)
                    ? new Color(0.11f, 0.12f, 0.16f, 0.7f)
                    : new Color(0.07f, 0.08f, 0.10f, 0.7f);
                EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, screenY0, plotWidth, h), laneBg);
            }

            int[] fifthMidis = { 43, 55, 67, 79 };
            string[] fifthLabels = { "G2", "G3", "G4", "G5" };
            for (int i = 0; i < fifthMidis.Length; i++)
            {
                float normY = (float) ((fifthMidis[i] - minY) / yRange);
                float y = rect.y + paddingTop + plotHeight - (normY * plotHeight);
                EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, y, plotWidth, 1), new Color(0.20f, 0.22f, 0.28f, 0.35f));
                var fifthStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.40f, 0.44f, 0.52f, 0.7f) },
                    fontSize = 8
                };
                GUI.Label(new Rect(rect.x + 8, y - 8, paddingLeft - 10, 16), fifthLabels[i], fifthStyle);
            }

            int[] octaveMidis = { 36, 48, 60, 72, 84 };
            string[] octaveLabels = { "C2", "C3", "C4", "C5", "C6" };
            for (int i = 0; i < octaveMidis.Length; i++)
            {
                float normY = (float) ((octaveMidis[i] - minY) / yRange);
                float y = rect.y + paddingTop + plotHeight - (normY * plotHeight);
                EditorGUI.DrawRect(new Rect(rect.x + paddingLeft, y, plotWidth, 1), new Color(0.28f, 0.32f, 0.40f, 0.8f));
                var octaveStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    normal = { textColor = new Color(0.70f, 0.75f, 0.88f, 1f) }
                };
                float labelY = i == 0 ? y - 14 : (i == octaveMidis.Length - 1 ? y - 4 : y - 9);
                GUI.Label(new Rect(rect.x, labelY, paddingLeft - 4, 18), octaveLabels[i], octaveStyle);
            }

            const int NUM_V_DIVS = 5;
            for (int i = 0; i <= NUM_V_DIVS; i++)
            {
                float normX = (float) i / NUM_V_DIVS;
                float x = rect.x + paddingLeft + (normX * plotWidth);
                double timeValue = minTime + (normX * (maxTime - minTime));

                EditorGUI.DrawRect(new Rect(x, rect.y + paddingTop, 1, plotHeight), new Color(0.18f, 0.20f, 0.25f, 0.5f));
                GUI.Label(new Rect(x - 25, rect.y + paddingTop + plotHeight + 2, 50, 16), $"{timeValue:F1}s", EditorStyles.centeredGreyMiniLabel);
            }

            Handles.color = new Color(0.25f, 0.28f, 0.35f, 1f);
            Handles.DrawPolyLine(
                new Vector3(rect.x + paddingLeft, rect.y + paddingTop, 0),
                new Vector3(rect.x + paddingLeft, rect.y + paddingTop + plotHeight, 0),
                new Vector3(rect.x + paddingLeft + plotWidth, rect.y + paddingTop + plotHeight, 0)
            );

            for (int i = 0; i < windowSamples.Count; i++)
            {
                if (windowSamples[i].IsHit)
                {
                    float normX = (float) ((windowSamples[i].RealTime - minTime) / (maxTime - minTime));
                    float screenX = rect.x + paddingLeft + (normX * plotWidth);

                    EditorGUI.DrawRect(new Rect(screenX - 1f, rect.y + paddingTop + plotHeight - 16, 2, 16), new Color(0.2f, 0.95f, 0.45f, 0.85f));
                    EditorGUI.DrawRect(new Rect(screenX - 3.5f, rect.y + paddingTop + plotHeight - 18, 7, 3), new Color(0.25f, 1f, 0.5f, 0.95f));
                }
            }

            var currentSegment = new List<Vector3>();
            for (int i = 0; i < windowSamples.Count; i++)
            {
                var s = windowSamples[i];
                float normX = (float) ((s.RealTime - minTime) / (maxTime - minTime));
                float screenX = rect.x + paddingLeft + (normX * plotWidth);

                if (s.IsVoiced && s.MidiNote >= minY && s.MidiNote <= maxY)
                {
                    float normY = Mathf.Clamp01((float) ((s.MidiNote - minY) / yRange));
                    float screenY = rect.y + paddingTop + plotHeight - (normY * plotHeight);
                    currentSegment.Add(new Vector3(screenX, screenY, 0));
                }
                else
                {
                    DrawPitchSegment(currentSegment);
                    currentSegment.Clear();
                }
            }

            DrawPitchSegment(currentSegment);

            if (windowSamples.Count >= 2)
            {
                DrawMicGraphHoverCrosshair(rect, minTime, maxTime, paddingLeft, paddingTop, plotWidth, plotHeight, windowSamples);
            }
        }

        private static void DrawPitchSegment(List<Vector3> segment)
        {
            if (segment.Count > 1)
            {
                var points = segment.ToArray();
                Handles.color = new Color(0f, 0.85f, 1f, 0.25f);
                Handles.DrawAAPolyLine(5f, points);
                Handles.color = new Color(0.15f, 0.9f, 1f, 1f);
                Handles.DrawAAPolyLine(2.2f, points);

                for (int p = 0; p < points.Length; p++)
                {
                    var pt = points[p];
                    EditorGUI.DrawRect(new Rect(pt.x - 1f, pt.y - 1f, 2f, 2f), new Color(0.4f, 1f, 1f, 0.85f));
                }
            }
            else if (segment.Count == 1)
            {
                var pt = segment[0];
                EditorGUI.DrawRect(new Rect(pt.x - 3f, pt.y - 3f, 6f, 6f), new Color(0f, 0.85f, 1f, 0.35f));
                EditorGUI.DrawRect(new Rect(pt.x - 1.5f, pt.y - 1.5f, 3f, 3f), new Color(0.3f, 1f, 1f, 1f));
            }
        }

        private void DrawMicGraphHoverCrosshair(Rect rect, double minTime, double maxTime, float paddingLeft, float paddingTop, float plotWidth, float plotHeight, List<MicSample> windowSamples)
        {
            Vector2 mousePos = Event.current.mousePosition;
            if (!rect.Contains(mousePos))
            {
                return;
            }

            float plotX = mousePos.x - (rect.x + paddingLeft);
            if (plotX < 0 || plotX > plotWidth || windowSamples.Count < 2)
            {
                return;
            }

            float normX = plotX / plotWidth;
            double targetTime = minTime + (normX * (maxTime - minTime));

            int closestIdx = 0;
            double minDiff = double.MaxValue;
            for (int i = 0; i < windowSamples.Count; i++)
            {
                double diff = Math.Abs(windowSamples[i].RealTime - targetTime);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closestIdx = i;
                }
            }

            var sample = windowSamples[closestIdx];
            float sampleNormX = (float) ((sample.RealTime - minTime) / (maxTime - minTime));
            float sampleScreenX = rect.x + paddingLeft + (sampleNormX * plotWidth);

            Handles.color = new Color(1f, 1f, 1f, 0.4f);
            Handles.DrawLine(
                new Vector3(sampleScreenX, rect.y + paddingTop, 0),
                new Vector3(sampleScreenX, rect.y + paddingTop + plotHeight, 0)
            );

            string noteLabel = "--";
            if (sample.IsVoiced && sample.MidiNote > 0)
            {
                int roundedMidi = (int) MathF.Round(sample.MidiNote);
                int noteIndex = ((roundedMidi % 12) + 12) % 12;
                int octave = (roundedMidi / 12) - 1;
                float cents = (sample.MidiNote - roundedMidi) * 100f;
                noteLabel = $"{NOTE_NAMES[noteIndex]}{octave} ({cents:+0;-0;0}c)";
            }

            string tooltip = $"Time: {sample.RealTime:F2}s\nNote: {noteLabel}\nMIDI: {(sample.IsVoiced ? sample.MidiNote.ToString("F2") : "--")}\nLevel: {sample.VolumeDb:F1} dB\nHit: {(sample.IsHit ? "YES" : "NO")}";

            var tooltipContent = new GUIContent(tooltip);
            var tooltipStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };

            Vector2 tooltipSize = tooltipStyle.CalcSize(tooltipContent) + new Vector2(10, 6);
            float tooltipX = sampleScreenX + 12;
            if (tooltipX + tooltipSize.x > rect.x + rect.width - 6)
            {
                tooltipX = sampleScreenX - tooltipSize.x - 12;
            }

            float tooltipY = Mathf.Clamp(mousePos.y - (tooltipSize.y / 2), rect.y + paddingTop, rect.y + paddingTop + plotHeight - tooltipSize.y);
            GUI.Box(new Rect(tooltipX, tooltipY, tooltipSize.x, tooltipSize.y), tooltipContent, tooltipStyle);
        }

        private void DrawMicHudRibbon()
        {
            string devName = CleanDeviceName(_selectedMicDevice?.DisplayName);
            string devText = _activeMicDevice != null ? devName : "INACTIVE";
            Color devColor = _activeMicDevice != null ? new Color(0.2f, 0.85f, 0.4f) : Color.gray;

            string noteText = _micIsVoiced ? $"{_micCurrentNoteName} ({_micCurrentCents:+0;-0;0}c)" : "--";
            Color noteColor = _micIsVoiced ? (MathF.Abs(_micCurrentCents) < 10f ? new Color(0.25f, 0.95f, 0.45f) : new Color(0f, 0.85f, 1f)) : Color.gray;

            string dbText = $"{_micCurrentDb:F1} dB";
            Color dbColor = _micCurrentDb > 42f ? new Color(1f, 0.35f, 0.35f) : (_micCurrentDb > 20f ? new Color(0.25f, 0.85f, 0.35f) : (_micCurrentDb > 2f ? new Color(0.95f, 0.75f, 0.2f) : Color.gray));

            double hitAge = EditorApplication.timeSinceStartup - _lastHitTime;
            string hitText = hitAge < 0.25 ? $"HIT! ({_totalHitCount})" : $"{_totalHitCount} Hits";
            Color hitColor = hitAge < 0.25 ? new Color(0.25f, 1f, 0.45f) : new Color(0.8f, 0.85f, 0.9f);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetricTile("ACTIVE INPUT", devText, devColor);
                DrawMetricTile("PITCH TRACK", noteText, noteColor);
                DrawMetricTile("LEVEL (RMS)", dbText, dbColor);
                DrawMetricTile("HIT DETECTIONS", hitText, hitColor);

                GUILayout.Space(6);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(88), GUILayout.Height(30)))
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Reset Hits", EditorStyles.miniButton, GUILayout.Width(88), GUILayout.Height(22)))
                    {
                        _totalHitCount = 0;
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private static void DrawMetricTile(string title, string value, Color valueColor)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.Height(30)))
            {
                var titleStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 8,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = new Color(0.60f, 0.65f, 0.72f) }
                };
                GUILayout.Label(title.ToUpperInvariant(), titleStyle, GUILayout.Height(10));

                var valStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = valueColor }
                };
                GUILayout.Label(value, valStyle, GUILayout.Height(14));
            }
        }

        private void DrawBottomDashboard()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawBottomLeftSidebar();

                GUILayout.Space(6);

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    switch (_selectedBottomTab)
                    {
                        case 0:
                            DrawSongSyncCard();
                            break;
                        case 1:
                            DrawStemMixerCard();
                            break;
                        case 2:
                            DrawOutputRoutingCard();
                            EditorGUILayout.Space(6);
                            DrawBufferDiagnosticsCard();
                            break;
                        case 3:
                            DrawMicrophoneStudioDashboard();
                            break;
                        case 4:
                            DrawClockDriftTestCard();
                            break;
                        case 5:
                            DrawMetronomeRoutingTestCard();
                            break;
                    }
                }
            }
        }

        private void DrawBottomLeftSidebar()
        {
            int channelCount = _bassSong?.Channels?.Count ?? 0;
            string stemBadge = channelCount > 0 ? $"({channelCount})" : "";
            string micBadge = _activeMicDevice != null ? "● Active" : "";
            string driftBadge = _isDriftTestRunning ? "● Running" : "";
            string metronomeBadge = _metronomeLoopRunning ? "● Playing" : "";

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(145)))
            {
                EditorGUILayout.LabelField("TOOLS", EditorStyles.miniBoldLabel);
                EditorGUILayout.Space(3);

                DrawSidebarTab(0, "⏱️ Song Sync", "", GraphMode.SyncConvergence);
                DrawSidebarTab(1, "🎚️ Stem Mixer", stemBadge, GraphMode.SyncConvergence);
                DrawSidebarTab(2, "🔊 Device & Buffer", "", GraphMode.PositionJitter);
                DrawSidebarTab(3, "🎤 Microphone", micBadge, GraphMode.MicPitchAndHits);

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("TESTS", EditorStyles.miniBoldLabel);
                EditorGUILayout.Space(3);

                DrawSidebarTab(4, "🧪 Clock Drift", driftBadge, GraphMode.ClockDrift);
                DrawSidebarTab(5, "🎯 Metronome & Routing", metronomeBadge, GraphMode.SyncConvergence);

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawSidebarTab(int index, string label, string badge, GraphMode graphMode)
        {
            bool isSelected = _selectedBottomTab == index;
            Rect itemRect = GUILayoutUtility.GetRect(10, 1000, 24, 24, GUILayout.ExpandWidth(true));

            bool isHovered = itemRect.Contains(Event.current.mousePosition);

            if (isSelected)
            {
                EditorGUI.DrawRect(itemRect, new Color(0.18f, 0.38f, 0.65f, 1f));
                EditorGUI.DrawRect(new Rect(itemRect.x, itemRect.y, 3, itemRect.height), new Color(0.35f, 0.75f, 1f, 1f));
            }
            else if (isHovered)
            {
                EditorGUI.DrawRect(itemRect, new Color(1f, 1f, 1f, 0.06f));
            }

            var labelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = isSelected ? Color.white : new Color(0.85f, 0.88f, 0.92f) }
            };

            GUI.Label(new Rect(itemRect.x + 8, itemRect.y, itemRect.width - (string.IsNullOrEmpty(badge) ? 12 : 55), itemRect.height), label, labelStyle);

            if (!string.IsNullOrEmpty(badge))
            {
                var badgeStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = isSelected ? new Color(0.85f, 0.95f, 1f) : new Color(0.6f, 0.65f, 0.7f) }
                };
                GUI.Label(new Rect(itemRect.x + itemRect.width - 50, itemRect.y, 44, itemRect.height), badge, badgeStyle);
            }

            if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
            {
                if (_selectedBottomTab != index)
                {
                    _selectedBottomTab = index;
                    _graphMode = graphMode;
                    Repaint();
                }
                Event.current.Use();
            }

            EditorGUILayout.Space(2);
        }

        private void DrawMicrophoneStudioDashboard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawMicTopStatusBar();

                EditorGUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMicLevelStageColumn();

                    GUILayout.Space(6);

                    DrawMicTunerInstrumentColumn();

                    GUILayout.Space(6);

                    DrawMicMonitoringColumn();
                }

                if (!string.IsNullOrEmpty(_micStatusMessage) && (EditorApplication.timeSinceStartup - _lastMicStatusTime < 4.0))
                {
                    EditorGUILayout.Space(4);
                    var msgType = _micStatusIsError ? MessageType.Error : MessageType.Info;
                    EditorGUILayout.HelpBox(_micStatusMessage, msgType);
                }
            }
        }

        private void DrawMicTopStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool isConnected = _activeMicDevice != null;
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = isConnected ? new Color(0.2f, 0.85f, 0.4f, 1f) : new Color(0.45f, 0.5f, 0.55f, 1f);
                GUILayout.Label(isConnected ? " ● ACTIVE " : " ⏹ INACTIVE ", EditorStyles.miniButton, GUILayout.Width(75), GUILayout.Height(18));
                GUI.backgroundColor = prevBg;

                GUILayout.Space(6);

                string cleanDev = CleanDeviceName(_selectedMicDevice?.DisplayName);
                string devLabel = isConnected
                    ? $"🎤 {cleanDev}  (Channel {(_selectedMicDevice?.Channel ?? 0) + 1}/{_selectedMicDevice?.ChannelCount ?? 1})"
                    : "🎤 No microphone connected";

                var devLabelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 11
                };
                GUILayout.Label(devLabel, devLabelStyle, GUILayout.Height(18));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Select Device ▾", EditorStyles.toolbarDropDown, GUILayout.Width(125)))
                {
                    ShowMicDeviceMenu();
                }

                if (isConnected)
                {
                    if (GUILayout.Button("Disconnect", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    {
                        DisconnectMicrophone();
                    }
                }
                else if (_selectedMicDevice.HasValue)
                {
                    if (GUILayout.Button("Connect", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    {
                        ConnectMicrophone(_selectedMicDevice.Value);
                    }
                }
            }
        }

        private void DrawMicLevelStageColumn()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("🎚️ INPUT & GATE", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                DrawStudioVuMeter();

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Gate:", EditorStyles.miniLabel, GUILayout.Width(35));
                    float currentSens = SettingsManager.Settings?.MicrophoneSensitivity.Value ?? 2f;
                    float newSens = EditorGUILayout.Slider(currentSens, -50f, 50f);
                    if (SettingsManager.Settings != null && MathF.Abs(newSens - currentSens) > 0.01f)
                    {
                        SettingsManager.Settings.MicrophoneSensitivity.Value = newSens;
                    }
                }

                EditorGUILayout.Space(6);

                DrawHitTriggerPad();
            }
        }

        private void DrawStudioVuMeter()
        {
            Rect meterRect = GUILayoutUtility.GetRect(100, 1000, 18, 18);
            EditorGUI.DrawRect(meterRect, new Color(0.10f, 0.11f, 0.14f, 1f));

            float minDb = -20f;
            float maxDb = 50f;
            float rangeDb = maxDb - minDb;

            float normCurrent = Mathf.Clamp01((_micCurrentDb - minDb) / rangeDb);
            float normPeak = Mathf.Clamp01((_micPeakHoldDb - minDb) / rangeDb);
            float sensitivity = SettingsManager.Settings?.MicrophoneSensitivity.Value ?? 2f;
            float normSensitivity = Mathf.Clamp01((sensitivity - minDb) / rangeDb);

            float fillWidth = normCurrent * meterRect.width;

            if (fillWidth > 0)
            {
                float yellowSplit = Mathf.Clamp01((30f - minDb) / rangeDb);
                float redSplit = Mathf.Clamp01((42f - minDb) / rangeDb);

                float greenWidth = Math.Min(fillWidth, yellowSplit * meterRect.width);
                if (greenWidth > 0)
                {
                    EditorGUI.DrawRect(new Rect(meterRect.x, meterRect.y + 1, greenWidth, meterRect.height - 2), new Color(0.2f, 0.85f, 0.4f, 0.85f));
                }

                if (fillWidth > yellowSplit * meterRect.width)
                {
                    float yellowStartX = meterRect.x + (yellowSplit * meterRect.width);
                    float yellowWidth = Math.Min(fillWidth - (yellowSplit * meterRect.width), (redSplit - yellowSplit) * meterRect.width);
                    EditorGUI.DrawRect(new Rect(yellowStartX, meterRect.y + 1, yellowWidth, meterRect.height - 2), new Color(0.95f, 0.75f, 0.2f, 0.85f));
                }

                if (fillWidth > redSplit * meterRect.width)
                {
                    float redStartX = meterRect.x + (redSplit * meterRect.width);
                    float redWidth = fillWidth - (redSplit * meterRect.width);
                    EditorGUI.DrawRect(new Rect(redStartX, meterRect.y + 1, redWidth, meterRect.height - 2), new Color(1f, 0.3f, 0.3f, 0.9f));
                }
            }

            float gateX = meterRect.x + (normSensitivity * meterRect.width);
            EditorGUI.DrawRect(new Rect(gateX - 1, meterRect.y, 2, meterRect.height), new Color(1f, 0.65f, 0.15f, 0.95f));

            if (normPeak > 0)
            {
                float peakX = meterRect.x + (normPeak * meterRect.width);
                EditorGUI.DrawRect(new Rect(peakX - 1, meterRect.y, 2, meterRect.height), new Color(1f, 1f, 1f, 0.9f));
            }

            string meterText = $"{_micCurrentDb:F1} dB  (Peak: {_micPeakHoldDb:F1} dB  |  Gate: {sensitivity:F1} dB)";
            var textStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.95f, 0.95f, 0.95f, 0.9f) },
                fontSize = 9
            };
            GUI.Label(meterRect, meterText, textStyle);
        }

        private void DrawHitTriggerPad()
        {
            double hitAge = EditorApplication.timeSinceStartup - _lastHitTime;
            bool isHitFlashing = hitAge < 0.22;

            Rect padRect = GUILayoutUtility.GetRect(100, 1000, 24, 24);
            Color padBg = isHitFlashing
                ? new Color(0.2f, 0.95f, 0.45f, 0.9f)
                : new Color(0.16f, 0.18f, 0.22f, 0.9f);
            EditorGUI.DrawRect(padRect, padBg);

            var padStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = isHitFlashing ? Color.black : new Color(0.75f, 0.8f, 0.9f) },
                fontSize = 11
            };
            string padText = isHitFlashing ? "🥁 HIT DETECTED!" : $"🥁 Hit Trigger ({_totalHitCount} hits)";
            GUI.Label(padRect, padText, padStyle);

            if (Event.current.type == EventType.MouseDown && padRect.Contains(Event.current.mousePosition))
            {
                _totalHitCount = 0;
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawMicTunerInstrumentColumn()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("🎵 VOCAL TUNER", EditorStyles.boldLabel);
                EditorGUILayout.Space(1);

                string note = _micIsVoiced ? _micCurrentNoteName : "--";
                bool inTune = _micIsVoiced && MathF.Abs(_micCurrentCents) < 10f;
                Color noteColor = !_micIsVoiced
                    ? new Color(0.45f, 0.48f, 0.55f)
                    : (inTune ? new Color(0.25f, 0.95f, 0.45f) : new Color(0.15f, 0.85f, 1f));

                var heroStyle = new GUIStyle(EditorStyles.largeLabel)
                {
                    fontSize = 28,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = noteColor }
                };
                EditorGUILayout.LabelField(note, heroStyle, GUILayout.Height(32));

                DrawCentDeviationBar(inTune);

                EditorGUILayout.Space(2);

                string subtext = _micIsVoiced
                    ? $"{_micCurrentPitchHz:F1} Hz  •  MIDI {_micCurrentMidi:F1}  •  {_micCurrentCents:+0;-0;0}c"
                    : "Sing or speak into mic";
                var subStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 9,
                    alignment = TextAnchor.MiddleCenter
                };
                EditorGUILayout.LabelField(subtext, subStyle);
            }
        }

        private void DrawCentDeviationBar(bool inTune)
        {
            Rect barRect = GUILayoutUtility.GetRect(100, 1000, 14, 14);
            EditorGUI.DrawRect(barRect, new Color(0.12f, 0.13f, 0.16f, 1f));

            float centerX = barRect.x + (barRect.width * 0.5f);

            float sweetSpotWidth = barRect.width * 0.20f;
            EditorGUI.DrawRect(new Rect(centerX - (sweetSpotWidth * 0.5f), barRect.y, sweetSpotWidth, barRect.height), new Color(0.2f, 0.8f, 0.4f, 0.15f));
            EditorGUI.DrawRect(new Rect(centerX - 1, barRect.y, 2, barRect.height), new Color(1f, 1f, 1f, 0.45f));

            if (_micIsVoiced)
            {
                float normCents = Mathf.Clamp(_micCurrentCents / 50f, -1f, 1f);
                float needleX = centerX + (normCents * (barRect.width * 0.48f));
                Color needleColor = inTune
                    ? new Color(0.25f, 0.95f, 0.45f, 1f)
                    : new Color(1f, 0.65f, 0.15f, 1f);
                EditorGUI.DrawRect(new Rect(needleX - 2, barRect.y + 1, 4, barRect.height - 2), needleColor);
            }
        }

        private void DrawMicMonitoringColumn()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("🎧 MONITOR & HEALTH", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                EditorGUI.BeginChangeCheck();
                _micMonitoringEnabled = EditorGUILayout.ToggleLeft("Direct Monitoring", _micMonitoringEnabled, EditorStyles.boldLabel);
                if (EditorGUI.EndChangeCheck())
                {
                    _activeMicDevice?.SetMonitoringLevel(_micMonitoringEnabled ? _micMonitoringVolume : 0f);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Vol:", EditorStyles.miniLabel, GUILayout.Width(28));
                    EditorGUI.BeginChangeCheck();
                    _micMonitoringVolume = EditorGUILayout.Slider(_micMonitoringVolume, 0f, 1f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _activeMicDevice?.SetMonitoringLevel(_micMonitoringEnabled ? _micMonitoringVolume : 0f);
                    }
                }

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"{_micFps:F0} FPS • {_micFrameIntervalMs:F1}ms", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Reset Stream", EditorStyles.miniButton, GUILayout.Width(105), GUILayout.Height(18)))
                    {
                        int result = _activeMicDevice?.Reset() ?? 0;
                        _micStatusMessage = result == 0 ? "Stream reset OK" : $"Reset error: {result}";
                        _micStatusIsError = result != 0;
                        _lastMicStatusTime = EditorApplication.timeSinceStartup;
                    }
                }
            }
        }

        private void ShowMicDeviceMenu()
        {
            RefreshAvailableMicrophones();
            var menu = new GenericMenu();

            if (_availableMicDevices.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No Microphones or Input Devices Found"));
                menu.ShowAsContext();
                return;
            }

            var sharedMics = _availableMicDevices.Where(d => !d.DisplayName.StartsWith("ASIO: ", StringComparison.OrdinalIgnoreCase)).ToList();
            var asioMics = _availableMicDevices.Where(d => d.DisplayName.StartsWith("ASIO: ", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var device in sharedMics)
            {
                string devName = device.DisplayName;
                bool isCurrent = _activeMicDevice != null && _selectedMicDevice?.DisplayName == devName;
                var capturedDevice = device;
                menu.AddItem(new GUIContent($"Shared (WASAPI\\/DirectSound)/{devName}"), isCurrent, () =>
                {
                    ConnectMicrophone(capturedDevice);
                });
            }

            if (asioMics.Count > 0)
            {
                foreach (var device in asioMics)
                {
                    string devName = device.DisplayName;
                    string displayName = devName.StartsWith("ASIO: ", StringComparison.OrdinalIgnoreCase)
                        ? devName.Substring(6)
                        : devName;
                    bool isCurrent = _activeMicDevice != null && _selectedMicDevice?.DisplayName == devName;
                    var capturedDevice = device;
                    menu.AddItem(new GUIContent($"ASIO (Low Latency)/{displayName}"), isCurrent, () =>
                    {
                        ConnectMicrophone(capturedDevice);
                    });
                }
            }

            menu.ShowAsContext();
        }

        private void RefreshAvailableMicrophones()
        {
            try
            {
                _availableMicDevices.Clear();
                _availableMicDevices.AddRange(GlobalAudioHandler.GetAllInputDevices());
            }
            catch (Exception ex)
            {
                _micStatusMessage = $"Failed to scan input devices: {ex.Message}";
                _micStatusIsError = true;
                _lastMicStatusTime = EditorApplication.timeSinceStartup;
            }
        }

        private void ConnectMicrophone(InputDeviceInfo device)
        {
            DisconnectMicrophone();

            try
            {
                _activeMicDevice = GlobalAudioHandler.CreateInputDevice(device);
                if (_activeMicDevice == null)
                {
                    _micStatusMessage = $"Failed to initialize input '{device.DisplayName}'.";
                    _micStatusIsError = true;
                    _lastMicStatusTime = EditorApplication.timeSinceStartup;
                    return;
                }

                _selectedMicDevice = device;
                _activeMicDevice.IsRecordingOutput = true;
                _activeMicDevice.SetMonitoringLevel(_micMonitoringEnabled ? _micMonitoringVolume : 0f);
                _micStatusMessage = $"Connected to '{device.DisplayName}'";
                _micStatusIsError = false;
                _lastMicStatusTime = EditorApplication.timeSinceStartup;
            }
            catch (Exception ex)
            {
                _micStatusMessage = $"Microphone error: {ex.Message}";
                _micStatusIsError = true;
                _lastMicStatusTime = EditorApplication.timeSinceStartup;
            }

            Repaint();
        }

        private void DisconnectMicrophone()
        {
            if (_activeMicDevice != null)
            {
                _activeMicDevice.Dispose();
                _activeMicDevice = null;
            }

            _micCurrentDb = -160f;
            _micPeakDb = -160f;
            _micPeakHoldDb = -160f;
            _micCurrentPitchHz = 0f;
            _micCurrentMidi = 0f;
            _micCurrentNoteName = "--";
            _micCurrentCents = 0f;
            _micIsVoiced = false;
            _micFps = 0f;
            _micFpsFrameCount = 0;
            Repaint();
        }

        private void DrawOutputRoutingCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string currentDevice = SettingsManager.Settings?.OutputDevice.Value ?? "Default";
                var currentMode = GlobalAudioHandler.GetOutputMode(currentDevice);
                bool isAsio = currentMode == AudioOutputMode.Asio;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Output Routing & Driver", EditorStyles.boldLabel);

                    GUILayout.FlexibleSpace();

                    var prevBg = GUI.backgroundColor;
                    if (isAsio)
                    {
                        GUI.backgroundColor = new Color(0.2f, 0.75f, 1f, 1f);
                        GUILayout.Label(" ASIO (EXCLUSIVE) ", EditorStyles.helpBox, GUILayout.Height(18));
                    }
                    else
                    {
                        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f, 1f);
                        GUILayout.Label(" SHARED (WASAPI) ", EditorStyles.helpBox, GUILayout.Height(18));
                    }
                    GUI.backgroundColor = prevBg;

                    GUILayout.Space(4);

                    if (GUILayout.Button("Switch Device ▾", EditorStyles.miniButton, GUILayout.Width(125), GUILayout.Height(18)))
                    {
                        ShowDeviceMenu();
                    }
                }

                EditorGUILayout.Space(4);

                int sampleRate = Bass.Info.SampleRate;
                int speakerCount = Bass.Info.SpeakerCount;
                double latencyMs = GlobalAudioHandler.PlaybackLatency;
                var bufferInfo = GlobalAudioHandler.GetOutputBufferInfo();

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Active Device:", GUILayout.Width(90));
                    EditorGUILayout.SelectableLabel(currentDevice, EditorStyles.boldLabel, GUILayout.Height(18));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Format: {sampleRate} Hz, {speakerCount} ch", EditorStyles.miniLabel, GUILayout.Width(150));
                    EditorGUILayout.LabelField($"Latency: {latencyMs:F1} ms", EditorStyles.miniLabel, GUILayout.Width(110));

                    if (bufferInfo is { } info && info.PreferredLength > 0)
                    {
                        int bufferSamples = info.PreferredLength;
                        double bufferMs = info.SampleRate > 0 ? (bufferSamples * 1000.0 / info.SampleRate) : 0;
                        EditorGUILayout.LabelField($"Buffer: {bufferSamples} spl ({bufferMs:F1} ms)", EditorStyles.miniLabel);
                    }
                }

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (isAsio)
                    {
                        if (GUILayout.Button("Open ASIO Control Panel", EditorStyles.miniButton, GUILayout.Height(20)))
                        {
                            OpenAsioControlPanel();
                        }
                    }

                    if (GUILayout.Button("Restart / Re-link Output", EditorStyles.miniButton, GUILayout.Height(20)))
                    {
                        RestartOutput();
                    }

                    if (GUILayout.Button("🎯 Metronome & Routing Test", EditorStyles.miniButton, GUILayout.Height(20)))
                    {
                        _selectedBottomTab = 5;
                        Repaint();
                    }
                }

                if (!string.IsNullOrEmpty(_deviceStatusMessage) && (EditorApplication.timeSinceStartup - _lastDeviceStatusTime < 5.0))
                {
                    EditorGUILayout.Space(2);
                    var msgType = _deviceStatusIsError ? MessageType.Error : MessageType.Info;
                    EditorGUILayout.HelpBox(_deviceStatusMessage, msgType);
                }
            }
        }

        private void DrawSongSyncCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _modelSongSync = EditorGUILayout.ToggleLeft(" Enable Song Sync", _modelSongSync, EditorStyles.boldLabel, GUILayout.Width(150));

                    GUILayout.FlexibleSpace();

                    if (_bassSong != null && _modelSongSync)
                    {
                        var state = _audioSynchronizer?.State ?? AudioSynchronizer.SyncState.Idle;
                        float adj = _audioSynchronizer?.EffectiveAdjustment ?? 0f;
                        var prevBg = GUI.backgroundColor;
                        if (state == AudioSynchronizer.SyncState.Correcting)
                        {
                            GUI.backgroundColor = new Color(1f, 0.7f, 0.15f, 1f);
                            GUILayout.Label($" CORRECTING ({(adj * 100):+0.00;-0.00}%) ", EditorStyles.helpBox, GUILayout.Height(18));
                        }
                        else if (state == AudioSynchronizer.SyncState.Settling)
                        {
                            GUI.backgroundColor = new Color(0.3f, 0.75f, 1f, 1f);
                            GUILayout.Label(" SETTLING ", EditorStyles.helpBox, GUILayout.Height(18));
                        }
                        else
                        {
                            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.35f, 1f);
                            GUILayout.Label(" LOCKED (IDLE) ", EditorStyles.helpBox, GUILayout.Height(18));
                        }
                        GUI.backgroundColor = prevBg;
                    }
                }

                EditorGUILayout.Space(3);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Calibration:", GUILayout.Width(75));
                    float newCal = EditorGUILayout.Slider(_audioCalibrationMs, -200f, 200f);
                    if (Mathf.Abs(newCal - _audioCalibrationMs) > 0.01f)
                    {
                        _audioCalibrationMs = newCal;
                        _bassSong?.SetOutputLatency(_audioCalibrationMs / 1000.0);
                    }

                    if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(52)))
                    {
                        _audioCalibrationMs = 0f;
                        _bassSong?.SetOutputLatency(0.0);
                    }
                }

                if (_bassSong != null)
                {
                    float worstDelta = _audioSynchronizer?.WorstDelta * 1000f ?? 0f;
                    float effectiveSpeed = _playbackSpeed + (_audioSynchronizer?.EffectiveAdjustment ?? 0f);
                    float adjustment = _audioSynchronizer?.EffectiveAdjustment ?? 0f;

                    EditorGUILayout.Space(2);
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        var statStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.7f, 0.75f, 0.8f) } };
                        var valStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.35f, 0.9f, 0.5f) } };

                        EditorGUILayout.LabelField("Worst Error Δ:", statStyle, GUILayout.Width(78));
                        EditorGUILayout.LabelField($"{worstDelta:+0.0;-0.0;0.0} ms", valStyle, GUILayout.Width(50));
                        GUILayout.Space(8);

                        EditorGUILayout.LabelField("Effective Rate:", statStyle, GUILayout.Width(80));
                        EditorGUILayout.LabelField($"{effectiveSpeed:F3}x", valStyle, GUILayout.Width(45));
                        GUILayout.Space(8);

                        EditorGUILayout.LabelField("Sync Adj:", statStyle, GUILayout.Width(52));
                        EditorGUILayout.LabelField($"{(adjustment * 100):+0.0;-0.0}%", valStyle, GUILayout.Width(45));
                        GUILayout.FlexibleSpace();
                    }

                    EditorGUILayout.Space(3);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Step Jump:", GUILayout.Width(75));
                        if (GUILayout.Button("-100ms", EditorStyles.miniButtonLeft, GUILayout.Width(52))) _simulatedClockDisturbance -= 0.100;
                        if (GUILayout.Button("-50ms", EditorStyles.miniButtonMid, GUILayout.Width(46))) _simulatedClockDisturbance -= 0.050;
                        if (GUILayout.Button("-20ms", EditorStyles.miniButtonMid, GUILayout.Width(46))) _simulatedClockDisturbance -= 0.020;
                        if (GUILayout.Button("0ms", EditorStyles.miniButtonMid, GUILayout.Width(36))) _simulatedClockDisturbance = 0;
                        if (GUILayout.Button("+20ms", EditorStyles.miniButtonMid, GUILayout.Width(46))) _simulatedClockDisturbance += 0.020;
                        if (GUILayout.Button("+50ms", EditorStyles.miniButtonMid, GUILayout.Width(46))) _simulatedClockDisturbance += 0.050;
                        if (GUILayout.Button("+100ms", EditorStyles.miniButtonRight, GUILayout.Width(52))) _simulatedClockDisturbance += 0.100;

                        GUILayout.Space(6);
                        string distText = $"Δ: {_simulatedClockDisturbance * 1000:+0.0;-0.0;0.0}ms";
                        EditorGUILayout.LabelField(distText, EditorStyles.miniLabel, GUILayout.Width(55));

                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("⚡ GC Collect", EditorStyles.miniButton, GUILayout.Width(95)))
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Sim Drift:", GUILayout.Width(75));
                        _simulatedClockDriftPercent = EditorGUILayout.Slider(_simulatedClockDriftPercent, -2.0f, 2.0f);
                        if (GUILayout.Button("0%", EditorStyles.miniButton, GUILayout.Width(40)))
                        {
                            _simulatedClockDriftPercent = 0f;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Load a song to model input clock synchronization.", EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        private void DrawStemMixerCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(120)))
            {
                int channelCount = _bassSong?.Channels?.Count ?? 0;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Stem Mixer ({channelCount} Channels)", EditorStyles.boldLabel);

                    if (channelCount > 0)
                    {
                        bool anyReverb = _stemReverbs.Values.Any(r => r);
                        var prevBg = GUI.backgroundColor;
                        GUI.backgroundColor = anyReverb ? new Color(0.4f, 0.8f, 1f, 1f) : prevBg;
                        if (GUILayout.Button(anyReverb ? "★ All Reverb ON" : "☆ All Reverb OFF", EditorStyles.miniButton, GUILayout.Width(125)))
                        {
                            ToggleAllReverb(!anyReverb);
                        }
                        GUI.backgroundColor = prevBg;

                        if (GUILayout.Button("Reset All", EditorStyles.miniButton, GUILayout.Width(75)))
                        {
                            ResetStemControls();
                        }
                    }
                }

                EditorGUILayout.Space(4);

                if (_bassSong?.Channels == null || !_bassSong.Channels.Any())
                {
                    EditorGUILayout.LabelField("Load a multi-track song folder to mix individual stems.", EditorStyles.centeredGreyMiniLabel);
                    return;
                }

                bool anySolo = _stemSolos.Values.Any(s => s);
                var distinctStems = _bassSong.Channels.Select(c => c.Stem).Distinct();

                foreach (var stem in distinctStems)
                {
                    if (!_stemVolumes.ContainsKey(stem))
                    {
                        _stemVolumes[stem] = 1f;
                        _stemMutes[stem] = false;
                        _stemSolos[stem] = false;
                        _stemReverbs[stem] = false;
                    }

                    float currentVol = _stemVolumes[stem];
                    bool isMuted = _stemMutes[stem];
                    bool isSolo = _stemSolos[stem];
                    bool isReverb = _stemReverbs.TryGetValue(stem, out bool r) && r;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(stem.ToString(), EditorStyles.label, GUILayout.Width(75));

                        EditorGUI.BeginChangeCheck();
                        float newVol = GUILayout.HorizontalSlider(currentVol, 0f, 1f);
                        if (EditorGUI.EndChangeCheck())
                        {
                            _stemVolumes[stem] = newVol;
                            UpdateStemVolume(stem, anySolo);
                        }

                        EditorGUILayout.LabelField($"{(int) (newVol * 100f)}%", EditorStyles.miniLabel, GUILayout.Width(36));

                        var prevBg = GUI.backgroundColor;

                        GUI.backgroundColor = isMuted ? new Color(0.95f, 0.25f, 0.25f, 1f) : Color.white;
                        if (GUILayout.Button("M", EditorStyles.miniButtonLeft, GUILayout.Width(22), GUILayout.Height(18)))
                        {
                            _stemMutes[stem] = !_stemMutes[stem];
                            UpdateAllStemVolumes();
                            Repaint();
                        }

                        GUI.backgroundColor = isSolo ? new Color(1f, 0.75f, 0.1f, 1f) : Color.white;
                        if (GUILayout.Button("S", EditorStyles.miniButtonMid, GUILayout.Width(22), GUILayout.Height(18)))
                        {
                            bool isMultiSelect = Event.current.shift || Event.current.control;
                            if (isMultiSelect)
                            {
                                _stemSolos[stem] = !_stemSolos[stem];
                            }
                            else
                            {
                                int soloCount = _stemSolos.Values.Count(s => s);
                                bool alreadySolo = _stemSolos[stem];
                                if (alreadySolo && soloCount == 1)
                                {
                                    _stemSolos[stem] = false;
                                }
                                else
                                {
                                    foreach (var key in _stemSolos.Keys.ToList())
                                    {
                                        _stemSolos[key] = false;
                                    }
                                    _stemSolos[stem] = true;
                                }
                            }

                            UpdateAllStemVolumes();
                            Repaint();
                        }

                        GUI.backgroundColor = isReverb ? new Color(0.4f, 0.8f, 1f, 1f) : Color.white;
                        if (GUILayout.Button(new GUIContent("R", "Toggle Reverb (Starpower FX)"), EditorStyles.miniButtonRight, GUILayout.Width(22), GUILayout.Height(18)))
                        {
                            SetStemReverb(stem, !isReverb);
                            Repaint();
                        }

                        GUI.backgroundColor = prevBg;
                    }
                }
            }
        }

        private void DrawBufferDiagnosticsCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(120)))
            {
                EditorGUILayout.LabelField("Read-Ahead Buffer & Engine Health", EditorStyles.boldLabel);

                EditorGUILayout.Space(2);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Buffer Size:", GUILayout.Width(75));
                    int newBuffer = EditorGUILayout.IntSlider(_readAheadBufferMs, 0, 2000);
                    if (newBuffer != _readAheadBufferMs)
                    {
                        _readAheadBufferMs = newBuffer;
                        ApplyReadAheadBuffer(_readAheadBufferMs);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Presets:", GUILayout.Width(55));
                    DrawBufferPresetPill(0);
                    DrawBufferPresetPill(50);
                    DrawBufferPresetPill(100);
                    DrawBufferPresetPill(250);
                    DrawBufferPresetPill(500);
                    DrawBufferPresetPill(1000);
                }

                EditorGUILayout.Space(4);

                if (_bassSong != null)
                {
                    var stats = _bassSong.GetReadAheadStats();
                    double tempoLatency = _bassSong.GetTempoStreamLatency() * 1000.0;

                    if (stats.TargetFrames > 0)
                    {
                        float fillRatio = Mathf.Clamp01((float) stats.QueuedFrames / stats.TargetFrames);
                        Rect progressRect = GUILayoutUtility.GetRect(100, 16);
                        EditorGUI.ProgressBar(progressRect, fillRatio, $"Buffer Fill: {fillRatio * 100f:F0}% ({stats.QueuedFrames} / {stats.TargetFrames} frames)");
                    }

                    EditorGUILayout.Space(2);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var latencyColor = tempoLatency < 5.0 ? new Color(0.25f, 0.85f, 0.35f) : (tempoLatency < 25.0 ? new Color(0.95f, 0.75f, 0.2f) : new Color(1f, 0.35f, 0.35f));
                        var latencyStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = latencyColor } };
                        EditorGUILayout.LabelField($"Active: {_bassSong.ReadAheadBufferLength} ms  |  Latency: {tempoLatency:F1} ms", latencyStyle);

                        if (stats.UnderrunEvents > 0)
                        {
                            var warningStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = new Color(1f, 0.35f, 0.35f) } };
                            EditorGUILayout.LabelField($"⚠️ {stats.UnderrunEvents} Underruns", warningStyle, GUILayout.Width(95));
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Underruns: 0", EditorStyles.miniLabel, GUILayout.Width(75));
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No active audio stream.", EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        private void DrawBufferPresetPill(int ms)
        {
            bool isActive = _readAheadBufferMs == ms;
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button($"{ms}ms", EditorStyles.miniButton, GUILayout.Width(50), GUILayout.Height(18)))
            {
                SetBufferPreset(ms);
            }

            GUI.backgroundColor = prevBg;
        }

        private void SetBufferPreset(int bufferMs)
        {
            _readAheadBufferMs = bufferMs;
            ApplyReadAheadBuffer(_readAheadBufferMs);
        }

        private void ApplyReadAheadBuffer(int bufferMs)
        {
            GlobalAudioHandler.SetBufferLength(bufferMs);
            _bassSong?.SetReadAheadBuffer(bufferMs);
        }

        private void ToggleAllReverb(bool enable)
        {
            if (_bassSong?.Channels != null && _bassSong.Channels.Count > 0)
            {
                var distinctStems = _bassSong.Channels.Select(c => c.Stem).Distinct();
                foreach (var stem in distinctStems)
                {
                    SetStemReverb(stem, enable);
                }
            }
            else
            {
                foreach (var stem in ALL_STEMS)
                {
                    SetStemReverb(stem, enable);
                }
            }
            Repaint();
        }

        private void SetStemReverb(SongStem stem, bool enable)
        {
            if (stem == SongStem.Master)
            {
                return;
            }

            _stemReverbs[stem] = enable;
            GlobalAudioHandler.SetReverbSetting(stem, enable);
        }

        private void ResetStemControls()
        {
            StemSettings.ApplySettings = true;
            _stemVolumes.Clear();
            _stemMutes.Clear();
            _stemSolos.Clear();
            _stemReverbs.Clear();

            foreach (var stem in ALL_STEMS)
            {
                GlobalAudioHandler.SetVolumeSetting(stem, 1.0);
                GlobalAudioHandler.SetReverbSetting(stem, false);
            }

            if (_bassSong?.Channels == null)
            {
                return;
            }

            foreach (var channel in _bassSong.Channels)
            {
                _stemVolumes[channel.Stem] = 1f;
                _stemMutes[channel.Stem] = false;
                _stemSolos[channel.Stem] = false;
                _stemReverbs[channel.Stem] = false;
            }

            Repaint();
        }

        private void UpdateAllStemVolumes()
        {
            StemSettings.ApplySettings = true;
            bool anySolo = _stemSolos.Values.Any(s => s);

            foreach (var stem in ALL_STEMS)
            {
                if (_stemVolumes.ContainsKey(stem))
                {
                    UpdateStemVolume(stem, anySolo);
                }
                else
                {
                    GlobalAudioHandler.SetVolumeSetting(stem, anySolo ? 0.0 : 1.0);
                }
            }
        }

        private void UpdateStemVolume(SongStem stem, bool anySolo)
        {
            if (stem == SongStem.Master)
            {
                return;
            }

            StemSettings.ApplySettings = true;
            bool isMuted = _stemMutes.TryGetValue(stem, out bool m) && m;
            bool isSolo = _stemSolos.TryGetValue(stem, out bool s) && s;
            float baseVol = _stemVolumes.TryGetValue(stem, out float v) ? v : 1f;

            double effectiveVol;
            if (anySolo)
            {
                effectiveVol = isSolo && !isMuted ? baseVol : 0.0;
            }
            else
            {
                effectiveVol = isMuted ? 0.0 : baseVol;
            }

            GlobalAudioHandler.SetVolumeSetting(stem, effectiveVol);
        }

        private void InitializeLoadedSong(string songName, string sourcePath)
        {
            if (_bassSong == null)
            {
                return;
            }

            _audioSynchronizer = new AudioSynchronizer(_bassSong);
            _bassSong.SetReadAheadBuffer(_readAheadBufferMs);
            _bassSong.SetOutputLatency(_audioCalibrationMs / 1000.0);
            _bassSong.SetPosition(0);
            _bassSong.SongEnd += OnSongEnd;
            _loadedSongName = songName;
            _sourcePath = sourcePath;
            _playbackClock = 0;
            _simulatedClockDisturbance = 0;
            _simulatedClockDriftPercent = 0;
            _inputTimeOffset = InputManager.CurrentInputTime;
            _samples.Clear();
            _viewEndTime = -1;
            AddRecentPath(sourcePath);
            ResetStemControls();
        }

        private void LoadAudioFile(string filePath)
        {
            EnsureAudioInitialized();
            DisposeSong();

            var mixer = GlobalAudioHandler.LoadCustomFile(filePath, _playbackSpeed, _volume, normalize: false, SongStem.Song);
            _bassSong = mixer as BassSong;

            if (_bassSong != null)
            {
                InitializeLoadedSong(Path.GetFileName(filePath), filePath);
            }
            else
            {
                EditorUtility.DisplayDialog("Audio Load Failed", $"Failed to create mixer for audio file:\n{filePath}", "OK");
            }
        }

        private void LoadSongFolder(string folderPath)
        {
            EnsureAudioInitialized();
            DisposeSong();

            string songName = Path.GetFileName(folderPath);
            var mixer = GlobalAudioHandler.CreateMixer(songName, _playbackSpeed, _volume, clampStemVolume: false, normalize: false);
            if (mixer == null)
            {
                EditorUtility.DisplayDialog("Mixer Creation Failed", "Failed to allocate BASS StemMixer.", "OK");
                return;
            }

            string[] subFiles = Directory.GetFiles(folderPath);
            var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in subFiles)
            {
                fileMap[Path.GetFileName(file)] = file;
            }

            bool addedAny = false;
            foreach (string stem in IniAudio.SupportedStems)
            {
                var stemEnum = AudioHelpers.SupportedStems[stem];
                foreach (string format in IniAudio.SupportedFormats)
                {
                    string stemFileName = stem + format;
                    if (fileMap.TryGetValue(stemFileName, out string filePath))
                    {
                        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                        if (mixer.AddChannel(stream, stemEnum))
                        {
                            addedAny = true;
                            break;
                        }

                        stream.Dispose();
                    }
                }
            }

            if (!addedAny)
            {
                foreach (string file in subFiles)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (IniAudio.SupportedFormats.Contains(ext))
                    {
                        var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                        if (mixer.AddChannel(stream, SongStem.Song))
                        {
                            addedAny = true;
                            break;
                        }

                        stream.Dispose();
                    }
                }
            }

            if (addedAny)
            {
                _bassSong = mixer as BassSong;
                if (_bassSong != null)
                {
                    InitializeLoadedSong(songName, folderPath);
                }
            }
            else
            {
                mixer.Dispose();
                EditorUtility.DisplayDialog("Audio Load Failed", $"No supported audio stem files found in:\n{folderPath}", "OK");
            }
        }

        private void LoadSongEntry(SongEntry entry)
        {
            EnsureAudioInitialized();
            DisposeSong();

            var mixer = entry.LoadAudio(_playbackSpeed, _volume, SettingsManager.Settings?.CensorMatureContent.Value ?? false);
            _bassSong = mixer as BassSong;

            if (_bassSong != null)
            {
                InitializeLoadedSong($"{entry.Artist.Original} - {entry.Name.Original}", entry.ActualLocation);
            }
            else
            {
                EditorUtility.DisplayDialog("Audio Load Failed", $"Failed to load audio for song entry:\n{entry.Name.Original}", "OK");
            }
        }

        private void LoadRecentPaths()
        {
            string raw = EditorPrefs.GetString(RECENT_PATHS_KEY, string.Empty);
            _recentPaths = string.IsNullOrEmpty(raw)
                ? new List<string>()
                : raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private void AddRecentPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            _recentPaths.Remove(path);
            _recentPaths.Insert(0, path);
            if (_recentPaths.Count > MAX_RECENT_PATHS)
            {
                _recentPaths.RemoveRange(MAX_RECENT_PATHS, _recentPaths.Count - MAX_RECENT_PATHS);
            }

            EditorPrefs.SetString(RECENT_PATHS_KEY, string.Join("|", _recentPaths));
        }

        private void DrawFftTimelineMiniBar()
        {
            double songLength = _bassSong?.Length ?? 0;
            double currentPos = _bassSong?.GetPosition() ?? 0;

            if (songLength <= 0.05)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                Rect barRect = GUILayoutUtility.GetRect(100, 1000, 14, 14);
                EditorGUI.DrawRect(barRect, new Color(0.12f, 0.13f, 0.16f, 1f));

                float normPos = (float) Math.Clamp(currentPos / songLength, 0.0, 1.0);
                float barW = normPos * barRect.width;

                EditorGUI.DrawRect(new Rect(barRect.x, barRect.y + 1, barW, barRect.height - 2), new Color(0.15f, 0.85f, 0.95f, 0.5f));
                EditorGUI.DrawRect(new Rect(barRect.x + barW - 1, barRect.y, 2, barRect.height), new Color(0.4f, 1f, 1f, 1f));

                var evt = Event.current;
                if ((evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag) && barRect.Contains(evt.mousePosition))
                {
                    float clickedNorm = (evt.mousePosition.x - barRect.x) / barRect.width;
                    double targetPos = clickedNorm * songLength;
                    _bassSong?.SetPosition(targetPos);
                    evt.Use();
                    Repaint();
                }

                GUILayout.Space(6);
                string timeStr = $"{FormatTime(currentPos)} / {FormatTime(songLength)}";
                GUILayout.Label(timeStr, EditorStyles.miniLabel, GUILayout.Width(90), GUILayout.Height(14));
            }
        }

        private void DrawFftHudRibbon()
        {
            int sampleRate = Bass.Info.SampleRate > 0 ? Bass.Info.SampleRate : 44100;
            int fftPoints = 1 << _fftSizeLog;
            float binWidth = (sampleRate * 0.5f) / (fftPoints / 2f);

            string dominantPitch = _dominantFrequencyHz >= 20f
                ? $"{_dominantNoteName} ({_dominantFrequencyHz:F0} Hz)"
                : "--";
            Color peakColor = _dominantDb > -12f ? new Color(1f, 0.35f, 0.35f) : (_dominantDb > -30f ? new Color(0.25f, 0.95f, 0.45f) : new Color(0.2f, 0.75f, 1f));

            string dbText = _dominantDb > -150f ? $"{_dominantDb:+0.0;-0.0;0.0} dBFS" : "-∞ dB";
            string centroidText = _spectralCentroidHz > 10f ? $"{_spectralCentroidHz:F0} Hz" : "--";
            string resText = $"{fftPoints} pts ({binWidth:F1} Hz/bin)";

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetricTile("DOMINANT PEAK", dominantPitch, new Color(0.15f, 0.9f, 1f));
                DrawMetricTile("PEAK LEVEL", dbText, peakColor);
                DrawMetricTile("CENTROID (BRIGHTNESS)", centroidText, new Color(0.85f, 0.65f, 1f));
                DrawMetricTile("FFT RESOLUTION", resText, Color.white);
                DrawMetricTile("FRAME RATE", $"{_currentFps:F0} FPS", Color.white);

                GUILayout.Space(6);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(80), GUILayout.Height(36)))
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Reset Peaks", EditorStyles.miniButton, GUILayout.Width(80), GUILayout.Height(22)))
                    {
                        if (_peakFft != null)
                        {
                            for (int i = 0; i < _peakFft.Length; i++)
                            {
                                _peakFft[i] = _fftMinDb;
                            }
                        }
                        for (int b = 0; b < _fftBands.Length; b++)
                        {
                            _fftBands[b].PeakDb = _fftMinDb;
                        }
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawFftSpectrumGraph(Rect rect, Rect plotRect, float paddingLeft, float paddingTop, float paddingRight, float paddingBottom, float plotWidth, float plotHeight)
        {
            var evt = Event.current;
            if (evt.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(rect, new Color(0.06f, 0.07f, 0.09f, 1f));

            int sampleRate = Bass.Info.SampleRate > 0 ? Bass.Info.SampleRate : 44100;
            int fftPoints = 1 << _fftSizeLog;
            int binCount = fftPoints / 2;

            if (_fftBuffer == null || _fftBuffer.Length != binCount || _smoothedFft == null || _smoothedFft.Length != binCount || _peakFft == null || _peakFft.Length != binCount)
            {
                _fftBuffer = new float[binCount];
                _smoothedFft = new float[binCount];
                _peakFft = new float[binCount];
                for (int i = 0; i < binCount; i++)
                {
                    _peakFft[i] = _fftMinDb;
                }
            }

            for (int b = 0; b < _fftBands.Length; b++)
            {
                var band = _fftBands[b];
                float normX1 = GetFreqNorm(band.MinFreq);
                float normX2 = GetFreqNorm(band.MaxFreq);
                float x1 = plotRect.x + (normX1 * plotWidth);
                float x2 = plotRect.x + (normX2 * plotWidth);
                float bandW = Mathf.Max(1f, x2 - x1);

                Color zoneBg = band.BandColor;
                zoneBg.a = (b % 2 == 0) ? 0.045f : 0.025f;
                EditorGUI.DrawRect(new Rect(x1, plotRect.y, bandW, plotHeight), zoneBg);

                var bandTagStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 8,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = new Color(band.BandColor.r, band.BandColor.g, band.BandColor.b, 0.45f) }
                };
                GUI.Label(new Rect(x1 + 3, plotRect.y + 2, bandW - 4, 12), band.Name.ToUpperInvariant(), bandTagStyle);
            }

            float[] dbSteps = { 0f, -12f, -24f, -36f, -48f, -60f, -72f, -84f, -96f };
            for (int i = 0; i < dbSteps.Length; i++)
            {
                float db = dbSteps[i];
                if (db < _fftMinDb || db > _fftMaxDb)
                {
                    continue;
                }

                float normY = Mathf.Clamp01((db - _fftMinDb) / (_fftMaxDb - _fftMinDb));
                float y = plotRect.y + plotHeight - (normY * plotHeight);

                Color lineCol = Mathf.Approximately(db, 0f)
                    ? new Color(0.40f, 0.45f, 0.55f, 0.7f)
                    : new Color(0.18f, 0.20f, 0.25f, 0.5f);
                EditorGUI.DrawRect(new Rect(plotRect.x, y, plotWidth, 1), lineCol);

                var dbLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 9,
                    normal = { textColor = new Color(0.55f, 0.60f, 0.70f, 0.8f) }
                };
                GUI.Label(new Rect(rect.x, y - 8, paddingLeft - 4, 16), $"{db:0} dB", dbLabelStyle);
            }

            float[] freqSteps = { 30f, 60f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f, 20000f };
            string[] freqLabels = { "30", "60", "125", "250", "500", "1k", "2k", "4k", "8k", "16k", "20k" };
            for (int i = 0; i < freqSteps.Length; i++)
            {
                float f = freqSteps[i];
                float normX = GetFreqNorm(f);
                float x = plotRect.x + (normX * plotWidth);

                EditorGUI.DrawRect(new Rect(x, plotRect.y, 1, plotHeight), new Color(0.18f, 0.20f, 0.25f, 0.5f));

                var fLabelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 8
                };
                GUI.Label(new Rect(x - 18, plotRect.y + plotHeight + 3, 36, 16), freqLabels[i], fLabelStyle);
            }

            if (_fftDisplayStyle == FftDisplayStyle.RtaBars || _fftDisplayStyle == FftDisplayStyle.Both)
            {
                int numBars = 48;
                float barSpacing = 1.5f;
                float totalSlotWidth = plotWidth / numBars;
                float barWidth = Mathf.Max(1f, totalSlotWidth - barSpacing);

                for (int b = 0; b < numBars; b++)
                {
                    float u1 = b / (float) numBars;
                    float u2 = (b + 1) / (float) numBars;
                    float f1 = GetNormFreq(u1);
                    float f2 = GetNormFreq(u2);
                    float fCenter = Mathf.Sqrt(f1 * f2);

                    float mag = SampleFftMagnitude(fCenter, sampleRate, binCount);
                    float db = 20f * Mathf.Log10(Mathf.Max(mag, 1e-6f));
                    float normY = Mathf.Clamp01((db - _fftMinDb) / (_fftMaxDb - _fftMinDb));

                    float barH = normY * plotHeight;
                    float barX = plotRect.x + (b * totalSlotWidth) + (barSpacing * 0.5f);
                    float barY = plotRect.y + plotHeight - barH;

                    Color barColor = GetBandColorForFreq(fCenter);
                    if (_fftDisplayStyle == FftDisplayStyle.Both)
                    {
                        barColor.a = 0.35f;
                    }

                    EditorGUI.DrawRect(new Rect(barX, barY, barWidth, barH), barColor);

                    if (_fftPeakHoldEnabled && _peakFft != null)
                    {
                        float peakDb = SamplePeakFftDb(fCenter, sampleRate, binCount);
                        float peakNormY = Mathf.Clamp01((peakDb - _fftMinDb) / (_fftMaxDb - _fftMinDb));
                        float peakY = plotRect.y + plotHeight - (peakNormY * plotHeight);
                        EditorGUI.DrawRect(new Rect(barX, peakY - 1, barWidth, 2), new Color(1f, 0.85f, 0.3f, 0.85f));
                    }
                }
            }

            if (_fftDisplayStyle == FftDisplayStyle.FilledCurve || _fftDisplayStyle == FftDisplayStyle.Both)
            {
                int steps = Mathf.Clamp((int) (plotWidth / 2.5f), 100, 400);
                var curvePoints = new List<Vector3>(steps + 1);
                var peakPoints = new List<Vector3>(steps + 1);

                for (int s = 0; s <= steps; s++)
                {
                    float u = s / (float) steps;
                    float screenX = plotRect.x + (u * plotWidth);
                    float f = GetNormFreq(u);

                    float mag = SampleFftMagnitude(f, sampleRate, binCount);
                    float db = 20f * Mathf.Log10(Mathf.Max(mag, 1e-6f));
                    float normY = Mathf.Clamp01((db - _fftMinDb) / (_fftMaxDb - _fftMinDb));
                    float screenY = plotRect.y + plotHeight - (normY * plotHeight);

                    curvePoints.Add(new Vector3(screenX, screenY, 0));

                    float sliceH = (plotRect.y + plotHeight) - screenY;
                    if (sliceH > 1f && _fftDisplayStyle == FftDisplayStyle.FilledCurve)
                    {
                        float sliceW = (plotWidth / steps) + 0.5f;
                        Color fillCol = new Color(0.12f, 0.65f, 0.95f, (normY * 0.28f) + 0.03f);
                        EditorGUI.DrawRect(new Rect(screenX, screenY, sliceW, sliceH), fillCol);
                    }

                    if (_fftPeakHoldEnabled && _peakFft != null)
                    {
                        float peakDb = SamplePeakFftDb(f, sampleRate, binCount);
                        float peakNormY = Mathf.Clamp01((peakDb - _fftMinDb) / (_fftMaxDb - _fftMinDb));
                        float peakScreenY = plotRect.y + plotHeight - (peakNormY * plotHeight);
                        peakPoints.Add(new Vector3(screenX, peakScreenY, 0));
                    }
                }

                if (_fftPeakHoldEnabled && peakPoints.Count > 1)
                {
                    Handles.color = new Color(1f, 0.8f, 0.25f, 0.7f);
                    Handles.DrawAAPolyLine(1.5f, peakPoints.ToArray());
                }

                if (curvePoints.Count > 1)
                {
                    Handles.color = new Color(0.1f, 0.85f, 1f, 0.35f);
                    Handles.DrawAAPolyLine(4.5f, curvePoints.ToArray());
                    Handles.color = new Color(0.35f, 0.95f, 1f, 1f);
                    Handles.DrawAAPolyLine(2.0f, curvePoints.ToArray());
                }
            }

            Handles.color = new Color(0.25f, 0.28f, 0.35f, 1f);
            Handles.DrawPolyLine(
                new Vector3(plotRect.x, plotRect.y, 0),
                new Vector3(plotRect.x, plotRect.y + plotHeight, 0),
                new Vector3(plotRect.x + plotWidth, plotRect.y + plotHeight, 0)
            );

            if (_bassSong == null)
            {
                var hintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 11,
                    normal = { textColor = new Color(0.6f, 0.65f, 0.75f, 0.6f) }
                };
                GUI.Label(new Rect(plotRect.x, plotRect.y + (plotHeight * 0.35f), plotWidth, 24), "Load an audio file or song folder above and press Play to visualize spectrum", hintStyle);
            }
            else if (_bassSong.IsPaused && _dominantDb <= _fftMinDb + 1f)
            {
                var hintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 11,
                    normal = { textColor = new Color(0.6f, 0.65f, 0.75f, 0.6f) }
                };
                GUI.Label(new Rect(plotRect.x, plotRect.y + (plotHeight * 0.35f), plotWidth, 24), "Playback paused. Press Play to start live FFT analysis", hintStyle);
            }

            DrawFftHoverCrosshair(rect, plotRect, paddingLeft, paddingTop, plotWidth, plotHeight, sampleRate, binCount);
        }

        private void DrawFftHoverCrosshair(Rect rect, Rect plotRect, float paddingLeft, float paddingTop, float plotWidth, float plotHeight, int sampleRate, int binCount)
        {
            Vector2 mousePos = Event.current.mousePosition;
            if (!plotRect.Contains(mousePos))
            {
                return;
            }

            float plotX = mousePos.x - plotRect.x;
            float normX = Mathf.Clamp01(plotX / plotWidth);
            float freq = GetNormFreq(normX);

            float mag = SampleFftMagnitude(freq, sampleRate, binCount);
            float db = 20f * Mathf.Log10(Mathf.Max(mag, 1e-6f));
            float normY = Mathf.Clamp01((db - _fftMinDb) / (_fftMaxDb - _fftMinDb));
            float curY = plotRect.y + plotHeight - (normY * plotHeight);

            Handles.color = new Color(1f, 1f, 1f, 0.4f);
            Handles.DrawLine(new Vector3(mousePos.x, plotRect.y, 0), new Vector3(mousePos.x, plotRect.y + plotHeight, 0));
            Handles.color = new Color(1f, 1f, 1f, 0.2f);
            Handles.DrawLine(new Vector3(plotRect.x, curY, 0), new Vector3(plotRect.x + plotWidth, curY, 0));

            EditorGUI.DrawRect(new Rect(mousePos.x - 3f, curY - 3f, 6f, 6f), new Color(0.15f, 0.95f, 1f, 0.4f));
            EditorGUI.DrawRect(new Rect(mousePos.x - 1.5f, curY - 1.5f, 3f, 3f), new Color(0.5f, 1f, 1f, 1f));

            string noteText = "--";
            if (freq >= 20f)
            {
                float midi = FreqToMidi(freq);
                int roundedMidi = (int) MathF.Round(midi);
                int noteIndex = ((roundedMidi % 12) + 12) % 12;
                int octave = (roundedMidi / 12) - 1;
                float cents = (midi - roundedMidi) * 100f;
                noteText = $"{NOTE_NAMES[noteIndex]}{octave} ({cents:+0;-0;0}c)";
            }

            string bandName = GetBandNameForFreq(freq);
            string freqStr = freq >= 1000f ? $"{freq / 1000f:F2} kHz" : $"{freq:F0} Hz";
            string tooltip = $"Freq: {freqStr}\nNote: {noteText}\nLevel: {db:F1} dBFS\nBand: {bandName}";

            var tooltipContent = new GUIContent(tooltip);
            var tooltipStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };

            Vector2 tooltipSize = tooltipStyle.CalcSize(tooltipContent) + new Vector2(10, 6);
            float tooltipX = mousePos.x + 12;
            if (tooltipX + tooltipSize.x > rect.x + rect.width - 6)
            {
                tooltipX = mousePos.x - tooltipSize.x - 12;
            }

            float tooltipY = Mathf.Clamp(mousePos.y - (tooltipSize.y / 2), plotRect.y, plotRect.y + plotHeight - tooltipSize.y);
            GUI.Box(new Rect(tooltipX, tooltipY, tooltipSize.x, tooltipSize.y), tooltipContent, tooltipStyle);
        }



        private float SampleFftMagnitude(float freq, int sampleRate, int binCount)
        {
            if (_smoothedFft == null || _smoothedFft.Length == 0 || binCount <= 0)
            {
                return 0f;
            }

            float nyquist = sampleRate * 0.5f;
            float freqPerBin = nyquist / binCount;
            float binFloat = freq / freqPerBin;

            int binIndex = (int) binFloat;
            if (binIndex < 0)
            {
                return _smoothedFft[0];
            }
            if (binIndex >= _smoothedFft.Length - 1)
            {
                return _smoothedFft[_smoothedFft.Length - 1];
            }

            float frac = binFloat - binIndex;
            return Mathf.Lerp(_smoothedFft[binIndex], _smoothedFft[binIndex + 1], frac);
        }

        private float SamplePeakFftDb(float freq, int sampleRate, int binCount)
        {
            if (_peakFft == null || _peakFft.Length == 0 || binCount <= 0)
            {
                return _fftMinDb;
            }

            float nyquist = sampleRate * 0.5f;
            float freqPerBin = nyquist / binCount;
            float binFloat = freq / freqPerBin;

            int binIndex = (int) binFloat;
            if (binIndex < 0)
            {
                return _peakFft[0];
            }
            if (binIndex >= _peakFft.Length - 1)
            {
                return _peakFft[_peakFft.Length - 1];
            }

            float frac = binFloat - binIndex;
            return Mathf.Lerp(_peakFft[binIndex], _peakFft[binIndex + 1], frac);
        }

        private float GetFreqNorm(float freq)
        {
            if (_fftScaleMode == FftScaleMode.Linear)
            {
                return Mathf.Clamp01((freq - FFT_MIN_FREQ) / (FFT_MAX_FREQ - FFT_MIN_FREQ));
            }

            float clampedFreq = Mathf.Clamp(freq, FFT_MIN_FREQ, FFT_MAX_FREQ);
            return Mathf.Clamp01(Mathf.Log10(clampedFreq / FFT_MIN_FREQ) / Mathf.Log10(FFT_MAX_FREQ / FFT_MIN_FREQ));
        }

        private float GetNormFreq(float normX)
        {
            float clampedNorm = Mathf.Clamp01(normX);
            if (_fftScaleMode == FftScaleMode.Linear)
            {
                return FFT_MIN_FREQ + (clampedNorm * (FFT_MAX_FREQ - FFT_MIN_FREQ));
            }

            return FFT_MIN_FREQ * Mathf.Pow(FFT_MAX_FREQ / FFT_MIN_FREQ, clampedNorm);
        }

        private Color GetBandColorForFreq(float freq)
        {
            for (int i = 0; i < _fftBands.Length; i++)
            {
                if (freq >= _fftBands[i].MinFreq && freq < _fftBands[i].MaxFreq)
                {
                    return _fftBands[i].BandColor;
                }
            }
            return new Color(0.2f, 0.85f, 0.95f);
        }

        private string GetBandNameForFreq(float freq)
        {
            for (int i = 0; i < _fftBands.Length; i++)
            {
                if (freq >= _fftBands[i].MinFreq && freq < _fftBands[i].MaxFreq)
                {
                    return _fftBands[i].Name;
                }
            }
            return "Audio Band";
        }

        private static float FreqToMidi(float freq)
        {
            if (freq < 1e-4f)
            {
                return 0f;
            }

            return 69f + (12f * (float) (Math.Log(freq / 440.0) / 0.6931471805599453));
        }

        private static string FormatTime(double seconds)
        {
            if (seconds < 0)
            {
                seconds = 0;
            }

            int mins = (int) (seconds / 60);
            double secs = seconds % 60;
            int wholeSecs = (int) secs;
            int frac = (int) ((secs - wholeSecs) * 100);
            return $"{mins:00}:{wholeSecs:00}.{frac:02}";
        }

        private void DrawClockDriftTestCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    var prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = _isDriftTestRunning ? new Color(0.2f, 0.85f, 0.4f, 1f) : new Color(0.45f, 0.5f, 0.55f, 1f);
                    GUILayout.Label(_isDriftTestRunning ? " ● DRIFT TEST RUNNING " : " ⏹ DRIFT TEST STOPPED ", EditorStyles.miniButton, GUILayout.Width(160), GUILayout.Height(18));
                    GUI.backgroundColor = prevBg;

                    GUILayout.Space(6);
                    var titleStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft, fontSize = 10 };
                    GUILayout.Label("Hardware Clock Drift Calibration", titleStyle, GUILayout.Height(18));

                    GUILayout.FlexibleSpace();

                    if (_isDriftTestRunning)
                    {
                        var stopBg = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(1f, 0.35f, 0.35f, 1f);
                        if (GUILayout.Button("Stop Test", EditorStyles.toolbarButton, GUILayout.Width(78)))
                        {
                            StopDriftTest();
                        }
                        GUI.backgroundColor = stopBg;
                    }
                    else
                    {
                        var startBg = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(0.3f, 0.85f, 0.45f, 1f);
                        if (GUILayout.Button("Start Test", EditorStyles.toolbarButton, GUILayout.Width(78)))
                        {
                            StartDriftTest();
                        }
                        GUI.backgroundColor = startBg;
                    }
                }

                EditorGUILayout.Space(4);

                EditorGUILayout.HelpBox(
                    "How to run: Click 'Start Test' and let the audio stream play continuously for 1–2 minutes. Cumulative drift and estimated rate (ppm) will track whether the audio hardware DAC is staying locked with the system clock without runaway drift.",
                    MessageType.Info);

                EditorGUILayout.Space(4);

                string durationStr = FormatTime(_driftElapsedHostSeconds);
                Color driftColor = Math.Abs(_driftCumulativeMs) > 10.0 ? new Color(1f, 0.4f, 0.4f) : (Math.Abs(_driftCumulativeMs) > 3.0 ? new Color(1f, 0.75f, 0.2f) : new Color(0.25f, 0.95f, 0.45f));
                string driftStr = !_driftBaselineEstablished ? "STARTING..." : $"{_driftCumulativeMs:+0.00;-0.00;0.00} ms";
                string rateStr = !_driftBaselineEstablished || _driftElapsedHostSeconds < 1.0 ? "CALCULATING..." : $"{_driftRatePpm:+0.0;-0.0;0.0} ppm ({_driftMsPerMin:+0.00;-0.00;0.00} ms/min)";
                string outputRate = !_driftBaselineEstablished || _driftElapsedHostSeconds < 1.0 ? "CALCULATING..." : $"{_driftCallbackRatePpm:+0.0;-0.0;0.0} ppm";

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetricTile("DURATION", durationStr, Color.white);
                    DrawMetricTile("CUMULATIVE DRIFT", driftStr, driftColor);
                    DrawMetricTile("ESTIMATED RATE", rateStr, new Color(0.3f, 0.8f, 1f));
                    DrawMetricTile("CALLBACK RATE", outputRate, new Color(0.7f, 0.85f, 1f));
                }

                EditorGUILayout.Space(4);

                DrawDriftPhaseGauge();

                EditorGUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    var tipStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, normal = { textColor = new Color(0.7f, 0.75f, 0.8f) } };
                    GUILayout.Label("💡 Long-term drift measures high-precision host timer (QPC) against hardware audio crystal DAC rate over extended playback to quantify clock drift and stream stability.", tipStyle);
                }
            }
        }

        private void DrawMainOscilloscopeGraph(Rect rect, Rect plotRect, float paddingLeft, float paddingTop, float paddingRight, float paddingBottom, float plotWidth, float plotHeight)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(plotRect, new Color(0.06f, 0.07f, 0.09f, 1f));

            float centerY = plotRect.y + (plotHeight * 0.5f);

            float[] levels = { 1.0f, 0.5f, 0.0f, -0.5f, -1.0f };
            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.55f, 0.55f, 0.6f, 0.6f) },
                fontSize = 9,
            };

            foreach (float lvl in levels)
            {
                float y = centerY - (lvl * (plotHeight * 0.45f));
                Color lineCol = lvl == 0f ? new Color(0.2f, 0.85f, 0.4f, 0.4f) : new Color(1f, 1f, 1f, 0.06f);
                EditorGUI.DrawRect(new Rect(plotRect.x, y, plotWidth, 1), lineCol);
                GUI.Label(new Rect(rect.x + 4, y - 8, paddingLeft - 6, 16), $"{lvl:+0.0;-0.0;0.0}", labelStyle);
            }

            int divCount = 10;
            float divWidth = plotWidth / divCount;
            float msPerDiv = (_scopeTimebase * 1000f) / divCount;
            for (int d = 0; d <= divCount; d++)
            {
                float x = plotRect.x + (d * divWidth);
                EditorGUI.DrawRect(new Rect(x, plotRect.y, 1, plotHeight), new Color(1f, 1f, 1f, 0.04f));
                if (d % 2 == 0)
                {
                    GUI.Label(new Rect(x - 15, plotRect.yMax + 2, 30, 16), $"{d * msPerDiv:0.#}ms", labelStyle);
                }
            }

            if (_scopePcmBuffer == null || _scopePcmBuffer.Length < 2048)
            {
                _scopePcmBuffer = new float[4096];
            }

            bool isPlaying = _bassSong != null && !_bassSong.IsPaused;
            int samplesRead = 0;
            if (isPlaying)
            {
                samplesRead = _bassSong!.GetSampleData(_scopePcmBuffer);
            }

            if (samplesRead <= 0 || !isPlaying)
            {
                Handles.color = new Color(0.2f, 0.85f, 1f, 0.4f);
                Handles.DrawLine(new Vector3(plotRect.x, centerY, 0), new Vector3(plotRect.xMax, centerY, 0));

                var emptyStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = new Color(0.5f, 0.5f, 0.55f, 0.5f) },
                    alignment = TextAnchor.MiddleCenter,
                };
                GUI.Label(plotRect, isPlaying ? "Buffering audio stream..." : "Paused / Stopped", emptyStyle);
                return;
            }

            int sampleRate = Bass.Info.SampleRate > 0 ? Bass.Info.SampleRate : 44100;
            int windowSamples = Math.Min(samplesRead, (int) (sampleRate * _scopeTimebase));
            if (windowSamples < 10)
            {
                windowSamples = 10;
            }

            int triggerIdx = 0;
            for (int i = 0; i < Math.Min(samplesRead - windowSamples, 1024); i++)
            {
                if (_scopePcmBuffer[i] <= 0f && _scopePcmBuffer[i + 1] > 0f)
                {
                    triggerIdx = i;
                    break;
                }
            }

            int renderCount = Math.Min(windowSamples, samplesRead - triggerIdx);
            if (renderCount < 2)
            {
                return;
            }

            var points = new Vector3[renderCount];
            float peak = 0f;
            double sumSq = 0;

            for (int i = 0; i < renderCount; i++)
            {
                float s = _scopePcmBuffer[triggerIdx + i] * _scopeGain;
                float absVal = Math.Abs(s);
                if (absVal > peak)
                {
                    peak = absVal;
                }
                sumSq += s * s;

                float normX = i / (float) (renderCount - 1);
                float px = plotRect.x + (normX * plotWidth);
                float py = centerY - (Mathf.Clamp(s, -1.2f, 1.2f) * (plotHeight * 0.45f));
                points[i] = new Vector3(px, py, 0);
            }

            float rms = (float) Math.Sqrt(sumSq / renderCount);
            float peakDb = 20f * Mathf.Log10(Mathf.Max(peak / _scopeGain, 1e-5f));
            float rmsDb = 20f * Mathf.Log10(Mathf.Max(rms / _scopeGain, 1e-5f));

            Handles.color = new Color(0.15f, 0.95f, 0.65f, 0.25f);
            Handles.DrawAAPolyLine(4.5f, points);

            Handles.color = new Color(0.25f, 1f, 0.75f, 0.95f);
            Handles.DrawAAPolyLine(2.0f, points);

            var hudStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = new Color(0.85f, 0.85f, 0.9f, 0.9f) },
            };
            string hudText = $"Timebase: {_scopeTimebase * 1000f:0}ms | Gain: {_scopeGain:0}x | Peak: {peak / _scopeGain:0.00} ({peakDb:+0.0;-0.0;0.0} dBFS) | RMS: {rms / _scopeGain:0.00} ({rmsDb:+0.0;-0.0;0.0} dBFS)";
            GUI.Label(new Rect(plotRect.x + 8, plotRect.y + 4, plotWidth - 16, 16), hudText, hudStyle);
        }

        private void DrawDriftPhaseGauge()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Phase Alignment:", EditorStyles.miniBoldLabel, GUILayout.Width(105));

                Rect gaugeRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(18));
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(gaugeRect, new Color(0.07f, 0.08f, 0.11f, 1f));

                    float centerX = gaugeRect.x + (gaugeRect.width * 0.5f);

                    // Draw subtle grid subdivisions (-20ms, -10ms, +10ms, +20ms)
                    float maxRangeMs = 25f;
                    float[] ticks = { -20f, -10f, 10f, 20f };
                    foreach (float tick in ticks)
                    {
                        float tickX = centerX + ((tick / maxRangeMs) * ((gaugeRect.width * 0.5f) - 8));
                        EditorGUI.DrawRect(new Rect(tickX, gaugeRect.y + 3, 1, gaugeRect.height - 6), new Color(1f, 1f, 1f, 0.08f));
                    }

                    // Center zero reference line
                    EditorGUI.DrawRect(new Rect(centerX - 1, gaugeRect.y, 2, gaugeRect.height), new Color(1f, 1f, 1f, 0.4f));

                    float normOffset = Mathf.Clamp((float) (_driftCumulativeMs / maxRangeMs), -1f, 1f);
                    float markerX = centerX + (normOffset * ((gaugeRect.width * 0.5f) - 8));

                    Color markerColor = Math.Abs(_driftCumulativeMs) > 10.0
                        ? new Color(1f, 0.35f, 0.35f, 1f)
                        : (Math.Abs(_driftCumulativeMs) > 3.0 ? new Color(1f, 0.75f, 0.2f, 1f) : new Color(0.25f, 0.95f, 0.45f, 1f));

                    // Marker pill
                    Rect markerRect = new Rect(markerX - 4, gaugeRect.y + 2, 8, gaugeRect.height - 4);
                    EditorGUI.DrawRect(markerRect, markerColor);

                    var leftStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.65f, 0.7f, 0.7f) }, alignment = TextAnchor.MiddleLeft, fontSize = 8 };
                    var rightStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.65f, 0.7f, 0.7f) }, alignment = TextAnchor.MiddleRight, fontSize = 8 };
                    GUI.Label(new Rect(gaugeRect.x + 4, gaugeRect.y + 1, 95, gaugeRect.height), "◄ Lag (-25ms)", leftStyle);
                    GUI.Label(new Rect(gaugeRect.xMax - 99, gaugeRect.y + 1, 95, gaugeRect.height), "(+25ms) Lead ►", rightStyle);
                }

                string offsetText = $"{_driftCumulativeMs:+0.00;-0.00;0.00} ms";
                Color offsetColor = Math.Abs(_driftCumulativeMs) > 10.0
                    ? new Color(1f, 0.4f, 0.4f)
                    : (Math.Abs(_driftCumulativeMs) > 3.0 ? new Color(1f, 0.75f, 0.2f) : new Color(0.25f, 0.95f, 0.45f));

                var offsetStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    normal = { textColor = offsetColor },
                    alignment = TextAnchor.MiddleRight
                };
                EditorGUILayout.LabelField(offsetText, offsetStyle, GUILayout.Width(62));

                if (_driftMaxPositionStepMs > 0)
                {
                    EditorGUILayout.LabelField($"• Max Step: {_driftMaxPositionStepMs:0.00}ms", EditorStyles.miniLabel, GUILayout.Width(110));
                }
            }
        }

        private void StartDriftTest()
        {
            if (!Mathf.Approximately(_playbackSpeed, 1f))
            {
                EditorUtility.DisplayDialog("Drift Test Requires 1x", "Set playback speed to 1.0x before starting this test.", "OK");
                return;
            }

            EnsureAudioInitialized();
            DisposeSong();
            _isDriftTestRunning = true;
            _driftBaselineEstablished = false;
            _driftTestStartTime = EditorApplication.timeSinceStartup;
            _driftTestQpcStart = Stopwatch.GetTimestamp();
            _driftInitialAudioStart = 0;
            _driftElapsedHostSeconds = 0;
            _driftElapsedAudioSeconds = 0;
            _driftCumulativeMs = 0;
            _driftRatePpm = 0;
            _driftMsPerMin = 0;
            _driftAudioLoopCount = 0;
            _driftMeasurements.Clear();
            _driftStartRequestedFrames = 0;
            _driftCallbackRatePpm = 0;
            _driftMaxPositionStepMs = 0;
            _driftLargePositionStepCount = 0;
            _driftPreviousSampleTime = 0;
            _driftPreviousSongPosition = 0;
            _driftPreviousPositionValid = false;

            string audioPath = GetDriftAudioFilePath();
            if (File.Exists(audioPath))
            {
                LoadAudioFile(audioPath);
                if (_bassSong != null)
                {
                    _bassSong.SetVolume(0f);
                    _calibrationTrackLength = _bassSong.Length;
                    _bassSong.SetPosition(0);
                    PlaySong();
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Audio File Missing", $"Could not find {audioPath}", "OK");
                _isDriftTestRunning = false;
            }
        }

        private void StopDriftTest()
        {
            _isDriftTestRunning = false;
            if (_bassSong != null && !_bassSong.IsPaused)
            {
                _bassSong.Pause();
            }
        }

        private void UpdateDriftTest(double now, double dt)
        {
            if (!_isDriftTestRunning)
            {
                return;
            }

            double songPos = _bassSong != null ? _bassSong.GetPosition() : 0;
            var readAheadStats = _bassSong?.GetReadAheadStats() ?? default;
            int sampleRate = Bass.Info.SampleRate;

            if (!_driftBaselineEstablished)
            {
                if (_bassSong != null && !_bassSong.IsPaused && songPos >= 0.1)
                {
                    _driftTestQpcStart = Stopwatch.GetTimestamp();
                    _driftInitialAudioStart = songPos;
                    _driftStartRequestedFrames = readAheadStats.RequestedFrames;
                    _driftBaselineEstablished = true;
                    _driftPreviousSampleTime = 0;
                    _driftPreviousSongPosition = songPos;
                    _driftPreviousPositionValid = true;
                }
                else
                {
                    return;
                }
            }

            if (_bassSong != null)
            {
                _bassSong.SetVolume(0f);
            }

            long nowTicks = Stopwatch.GetTimestamp();
            _driftElapsedHostSeconds = (double) (nowTicks - _driftTestQpcStart) / Stopwatch.Frequency;
            _driftElapsedAudioSeconds = (_driftAudioLoopCount * _calibrationTrackLength) + (songPos - _driftInitialAudioStart);
            _driftCumulativeMs = (_driftElapsedHostSeconds - _driftElapsedAudioSeconds) * 1000.0;

            double positionStepResidualMs = 0;
            if (_driftPreviousPositionValid)
            {
                double elapsedDelta = _driftElapsedHostSeconds - _driftPreviousSampleTime;
                double positionDelta = songPos - _driftPreviousSongPosition;
                if (positionDelta < -0.5)
                {
                    _driftPreviousSampleTime = _driftElapsedHostSeconds;
                    _driftPreviousSongPosition = songPos;
                }
                else if (elapsedDelta > 0)
                {
                    positionStepResidualMs = (positionDelta - elapsedDelta) * 1000.0;
                    double absoluteResidual = Math.Abs(positionStepResidualMs);
                    _driftMaxPositionStepMs = Math.Max(_driftMaxPositionStepMs, absoluteResidual);
                    if (absoluteResidual > 1.0)
                    {
                        _driftLargePositionStepCount++;
                    }

                    _driftPreviousSampleTime = _driftElapsedHostSeconds;
                    _driftPreviousSongPosition = songPos;
                }
            }

            if (sampleRate > 0 && _driftElapsedHostSeconds >= 1.0 &&
                readAheadStats.RequestedFrames >= _driftStartRequestedFrames)
            {
                double outputElapsed = (readAheadStats.RequestedFrames - _driftStartRequestedFrames) / (double) sampleRate;
                _driftCallbackRatePpm = ((outputElapsed / _driftElapsedHostSeconds) - 1.0) * 1_000_000.0;
            }
            else
            {
                _driftCallbackRatePpm = 0;
            }

            _driftMeasurements.Add(new DriftMeasurement
            {
                HostSeconds = _driftElapsedHostSeconds,
                SongPositionSeconds = songPos,
                DriftMs = _driftCumulativeMs,
                PositionStepResidualMs = positionStepResidualMs,
                ConsumedFrames = readAheadStats.ConsumedFrames,
                RequestedFrames = readAheadStats.RequestedFrames,
                QueuedFrames = readAheadStats.QueuedFrames,
                PositionOutputFrame = readAheadStats.PositionOutputFrame,
                CallbackFrames = readAheadStats.CallbackFrames,
                CallbackElapsedFrames = readAheadStats.CallbackElapsedFrames,
                CallbackCorrectionFrames = readAheadStats.CallbackCorrectionFrames,
                CallbackClockOffsetFrames = readAheadStats.CallbackClockOffsetFrames,
                UnderrunFrames = readAheadStats.UnderrunFrames,
                UnderrunEvents = readAheadStats.UnderrunEvents
            });
            if (_driftMeasurements.Count > MAX_DRIFT_MEASUREMENTS)
            {
                _driftMeasurements.RemoveAt(0);
            }

            if (_driftElapsedHostSeconds >= 1.0 && _driftMeasurements.Count >= 5)
            {
                double sumT = 0;
                double sumD = 0;
                for (int i = 0; i < _driftMeasurements.Count; i++)
                {
                    sumT += _driftMeasurements[i].HostSeconds;
                    sumD += _driftMeasurements[i].DriftMs;
                }
                double meanT = sumT / _driftMeasurements.Count;
                double meanD = sumD / _driftMeasurements.Count;

                double num = 0;
                double denom = 0;
                for (int i = 0; i < _driftMeasurements.Count; i++)
                {
                    double dtSample = _driftMeasurements[i].HostSeconds - meanT;
                    num += dtSample * (_driftMeasurements[i].DriftMs - meanD);
                    denom += dtSample * dtSample;
                }

                double slopeMsPerSec = denom > 1e-6 ? num / denom : 0.0;
                _driftRatePpm = slopeMsPerSec * 1000.0;
                _driftMsPerMin = slopeMsPerSec * 60.0;
            }
            else
            {
                _driftRatePpm = 0;
                _driftMsPerMin = 0;
            }

            Repaint();
        }

        private string GetDriftAudioFilePath()
        {
            string tempFolder = Path.Combine(Application.temporaryCachePath, "YARG_DriftTest");
            Directory.CreateDirectory(tempFolder);

            string path = Path.Combine(tempFolder, "drift_test_silence.wav");
            if (!File.Exists(path) || new FileInfo(path).Length < 10000000)
            {
                CreateSilenceWavFile(path, 1800f, 44100);
            }
            return path;
        }

        private static void CreateSilenceWavFile(string path, float durationSec, int sampleRate)
        {
            int numSamples = (int) (sampleRate * durationSec);
            int subChunk2Size = numSamples * sizeof(short);

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
            using var writer = new BinaryWriter(fs);

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + subChunk2Size);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short) 1);
            writer.Write((short) 1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * sizeof(short));
            writer.Write((short) 2);
            writer.Write((short) 16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(subChunk2Size);

            const int BUFFER_SIZE = 8192;
            byte[] byteBuffer = new byte[BUFFER_SIZE * sizeof(short)];
            int samplesWritten = 0;

            while (samplesWritten < numSamples)
            {
                int count = Math.Min(BUFFER_SIZE, numSamples - samplesWritten);
                writer.Write(byteBuffer, 0, count * sizeof(short));
                samplesWritten += count;
            }
        }

        #region Metronome & Multi-Channel Routing Test Bench

        private void UpdateMetronome(double now, double dt)
        {
            float decayDb = (float) (dt * 65f);
            for (int i = 0; i < 4; i++)
            {
                if (_channelPairPeaks[i] > -96f)
                {
                    _channelPairPeaks[i] = Mathf.Max(-96f, _channelPairPeaks[i] - decayDb);
                }
                if (now - _channelPairPeakHoldTime[i] > 0.8)
                {
                    _channelPairPeakHold[i] = Mathf.Max(-96f, _channelPairPeakHold[i] - (decayDb * 1.5f));
                }
            }

            if (_metronomeLoopRunning)
            {
                double beatInterval = 60.0 / Mathf.Clamp(_metronomeBpm, 30f, 300f);
                if (_nextMetronomeBeatTime <= 0 || now >= _nextMetronomeBeatTime)
                {
                    if (_nextMetronomeBeatTime <= 0 || (now - _nextMetronomeBeatTime) > beatInterval * 2)
                    {
                        _nextMetronomeBeatTime = now;
                    }
                    _nextMetronomeBeatTime += beatInterval;

                    bool isHi = _metronomeCurrentBeat == 0;
                    TriggerMetronomeClick(isHi);

                    _metronomeCurrentBeat = (_metronomeCurrentBeat + 1) % Math.Max(1, _metronomeBeatsPerBar);
                    Repaint();
                }
            }

            if (_selectedBottomTab == 5)
            {
                Repaint();
            }
        }

        private void TriggerMetronomeClick(bool isHi)
        {
            _lastMetronomeClickTime = EditorApplication.timeSinceStartup;
            _lastMetronomeClickIsHi = isHi;
            _totalMetronomeClicks++;

            try
            {
                EnsureAudioInitialized();

                var pitch = isHi ? MetronomePitch.Hi : MetronomePitch.Lo;
                GlobalAudioHandler.PlayMetronomeSoundEffect(_testMetronomeSound, pitch);

                int targetPair = _metronomeTargetChannel switch
                {
                    3 => 1,
                    5 => 2,
                    7 => 3,
                    _ => 0
                };

                _channelPairPeaks[targetPair] = 0f;
                _channelPairPeakHold[targetPair] = 0f;
                _channelPairPeakHoldTime[targetPair] = EditorApplication.timeSinceStartup;

                if (_headphoneAuditionMode && targetPair != 0)
                {
                    _channelPairPeaks[0] = Mathf.Max(_channelPairPeaks[0], -4f);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Metronome trigger failed: {ex.Message}");
            }
        }

        private void SetMetronomeTargetChannel(int channelId)
        {
            _metronomeTargetChannel = channelId;
            if (SettingsManager.Settings != null)
            {
                SettingsManager.Settings.OutputChannelMetronome.Value = channelId;
            }
            else
            {
                GlobalAudioHandler.SetOutputChannel(SongStem.Metronome, channelId == -1 ? 1 : channelId);
            }
        }

        private void HandleTapTempo()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_tapTimes[0] > 0 && (now - _tapTimes[(_tapIndex + 3) % 4]) > 2.5)
            {
                Array.Clear(_tapTimes, 0, _tapTimes.Length);
                _tapIndex = 0;
            }

            _tapTimes[_tapIndex] = now;
            _tapIndex = (_tapIndex + 1) % 4;

            int validTaps = 0;
            double totalDelta = 0;
            for (int i = 1; i < 4; i++)
            {
                int prev = i - 1;
                if (_tapTimes[prev] > 0 && _tapTimes[i] > 0 && _tapTimes[i] > _tapTimes[prev])
                {
                    totalDelta += (_tapTimes[i] - _tapTimes[prev]);
                    validTaps++;
                }
            }

            if (validTaps > 0)
            {
                double avgDelta = totalDelta / validTaps;
                if (avgDelta > 0.1)
                {
                    _metronomeBpm = Mathf.Clamp((float) Math.Round(60.0 / avgDelta), 40f, 240f);
                }
            }
        }

        private void DrawMetronomeRoutingTestCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawMetronomeTopStatusBar();

                EditorGUILayout.Space(4);

                EditorGUILayout.HelpBox(
                    "How to run: Click 'Start Metronome' (or test pads) to generate clicks, then select a channel pair (e.g. Pair 2 for Drummer In-Ear) " +
                    "to verify routing and channel isolation on the VU meters. Enable 'Stereo Headphone Audition Mode' to preview multi-channel routing on standard 2-channel headphones.",
                    MessageType.Info);

                EditorGUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetronomeSequencerColumn();

                    GUILayout.Space(10);

                    DrawMetronomeRoutingAndMeterColumn();
                }
            }
        }

        private void DrawMetronomeTopStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = _metronomeLoopRunning ? new Color(0.2f, 0.85f, 0.45f, 1f) : new Color(0.45f, 0.5f, 0.55f, 1f);
                GUILayout.Label(_metronomeLoopRunning ? " ● PLAYING " : " ⏹ IDLE ", EditorStyles.miniButton, GUILayout.Width(85), GUILayout.Height(18));
                GUI.backgroundColor = prevBg;

                GUILayout.Space(8);

                string channelDesc = _metronomeTargetChannel switch
                {
                    3 => "Aux / In-Ear (Ch 3-4)",
                    5 => "Center / Sub (Ch 5-6)",
                    7 => "Surround (Ch 7-8)",
                    _ => "Main Front (Ch 1-2)"
                };

                var statusStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(0.85f, 0.88f, 0.92f) }
                };
                GUILayout.Label($"Route: {channelDesc}   •   Sample: {_testMetronomeSound}   •   Tempo: {_metronomeBpm:F0} BPM ({_metronomeBeatsPerBar}/4)", statusStyle, GUILayout.Height(18));

                GUILayout.FlexibleSpace();

                if (_headphoneAuditionMode)
                {
                    var audBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.2f, 0.75f, 1f, 1f);
                    GUILayout.Label(" 🎧 Auditioning in Stereo ", EditorStyles.helpBox, GUILayout.Height(18));
                    GUI.backgroundColor = audBg;
                    GUILayout.Space(4);
                }

                if (GUILayout.Button("Stop Audio", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    _metronomeLoopRunning = false;
                    _bassSong?.Pause();
                }
            }
        }

        private void DrawMetronomeSequencerColumn()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(360)))
            {
                EditorGUILayout.LabelField("Metronome & Rhythm", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                // Hero Play / Stop Transport Button
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = _metronomeLoopRunning ? new Color(0.92f, 0.35f, 0.35f, 1f) : new Color(0.2f, 0.82f, 0.45f, 1f);
                var heroBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                string loopBtnText = _metronomeLoopRunning ? "⏹  Stop Metronome" : "▶  Start Metronome";
                if (GUILayout.Button(loopBtnText, heroBtnStyle, GUILayout.Height(32)))
                {
                    _metronomeLoopRunning = !_metronomeLoopRunning;
                    if (_metronomeLoopRunning)
                    {
                        _metronomeCurrentBeat = 0;
                        _nextMetronomeBeatTime = EditorApplication.timeSinceStartup;
                    }
                }
                GUI.backgroundColor = prevBg;

                EditorGUILayout.Space(8);

                // Tempo Section
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Tempo", EditorStyles.miniBoldLabel, GUILayout.Width(45));

                    if (GUILayout.Button("-", EditorStyles.miniButtonLeft, GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        _metronomeBpm = Mathf.Clamp(_metronomeBpm - 1f, 40f, 240f);
                    }

                    _metronomeBpm = EditorGUILayout.Slider(_metronomeBpm, 40f, 240f);

                    if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        _metronomeBpm = Mathf.Clamp(_metronomeBpm + 1f, 40f, 240f);
                    }

                    GUILayout.Space(4);
                    if (GUILayout.Button("Tap", EditorStyles.miniButton, GUILayout.Width(42), GUILayout.Height(18)))
                    {
                        HandleTapTempo();
                    }
                }

                // Quick BPM presets
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawBpmPresetPill(60f);
                    DrawBpmPresetPill(90f);
                    DrawBpmPresetPill(120f);
                    DrawBpmPresetPill(140f);
                    DrawBpmPresetPill(160f);
                    DrawBpmPresetPill(180f);
                }

                EditorGUILayout.Space(8);

                // Time Signature & Beat Visualizer
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Time Signature", EditorStyles.miniBoldLabel, GUILayout.Width(95));
                    DrawTimeSigPill(4, "4/4");
                    DrawTimeSigPill(3, "3/4");
                    DrawTimeSigPill(6, "6/8");
                    DrawTimeSigPill(1, "1/4");
                }

                EditorGUILayout.Space(4);
                DrawMetronomeBeatVisualizer();

                EditorGUILayout.Space(10);

                // Sound Preset Selection
                EditorGUILayout.LabelField("Sound Sample", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetronomeSoundPill(MetronomeSample.Clap, "Clap");
                    DrawMetronomeSoundPill(MetronomeSample.Castanet, "Castanet");
                    DrawMetronomeSoundPill(MetronomeSample.Party, "Party");
                    DrawMetronomeSoundPill(MetronomeSample.Quartz, "Quartz");
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawMetronomeSoundPill(MetronomeSample.Sine, "Sine");
                    DrawMetronomeSoundPill(MetronomeSample.Square, "Square");
                    DrawMetronomeSoundPill(MetronomeSample.Trashcan, "Trashcan");
                }

                EditorGUILayout.Space(8);

                // Volume slider with clean formatted percentage
                using (new EditorGUILayout.HorizontalScope())
                {
                    int volPct = Mathf.RoundToInt(_metronomeVolume * 100f);
                    EditorGUILayout.LabelField($"Volume: {volPct}%", EditorStyles.miniBoldLabel, GUILayout.Width(82));
                    EditorGUI.BeginChangeCheck();
                    float newVol = EditorGUILayout.Slider(_metronomeVolume, 0f, 2f);
                    if (EditorGUI.EndChangeCheck() || MathF.Abs(newVol - _metronomeVolume) > 0.01f)
                    {
                        _metronomeVolume = newVol;
                        if (SettingsManager.Settings != null)
                        {
                            SettingsManager.Settings.MetronomeVolume.Value = newVol;
                        }
                        GlobalAudioHandler.SetVolumeMultiplier(SongStem.Metronome, newVol);
                    }
                }

                EditorGUILayout.Space(6);

                // Manual Preview / Test Pads
                using (new EditorGUILayout.HorizontalScope())
                {
                    double hitAge = EditorApplication.timeSinceStartup - _lastMetronomeClickTime;
                    bool flashHi = hitAge < 0.15 && _lastMetronomeClickIsHi;
                    bool flashLo = hitAge < 0.15 && !_lastMetronomeClickIsHi;

                    var padPrevBg = GUI.backgroundColor;
                    GUI.backgroundColor = flashHi ? new Color(0.3f, 0.95f, 0.55f, 1f) : new Color(0.25f, 0.55f, 0.38f, 1f);
                    if (GUILayout.Button(flashHi ? "● Downbeat (Hi)" : "🔊 Test Downbeat", GUILayout.Height(24)))
                    {
                        TriggerMetronomeClick(true);
                    }

                    GUI.backgroundColor = flashLo ? new Color(0.35f, 0.85f, 1f, 1f) : new Color(0.28f, 0.48f, 0.65f, 1f);
                    if (GUILayout.Button(flashLo ? "● Upbeat (Lo)" : "🔉 Test Upbeat", GUILayout.Height(24)))
                    {
                        TriggerMetronomeClick(false);
                    }
                    GUI.backgroundColor = padPrevBg;
                }
            }
        }

        private void DrawMetronomeSoundPill(MetronomeSample sample, string label)
        {
            bool isSelected = _testMetronomeSound == sample;
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = isSelected ? new Color(0.25f, 0.75f, 1f, 1f) : new Color(0.32f, 0.35f, 0.40f, 0.75f);
            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Height(20)))
            {
                _testMetronomeSound = sample;
                if (SettingsManager.Settings != null)
                {
                    SettingsManager.Settings.MetronomeSound.Value = sample;
                }
                TriggerMetronomeClick(true);
            }
            GUI.backgroundColor = prevBg;
        }

        private void DrawBpmPresetPill(float bpm)
        {
            bool isSelected = MathF.Abs(_metronomeBpm - bpm) < 0.5f;
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = isSelected ? new Color(0.25f, 0.85f, 0.5f, 1f) : new Color(0.32f, 0.35f, 0.40f, 0.75f);
            if (GUILayout.Button($"{bpm:F0}", EditorStyles.miniButton, GUILayout.Height(18)))
            {
                _metronomeBpm = bpm;
            }
            GUI.backgroundColor = prevBg;
        }

        private void DrawTimeSigPill(int beats, string label)
        {
            bool isSelected = _metronomeBeatsPerBar == beats;
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = isSelected ? new Color(0.95f, 0.75f, 0.25f, 1f) : new Color(0.32f, 0.35f, 0.40f, 0.75f);
            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Height(18)))
            {
                _metronomeBeatsPerBar = beats;
                _metronomeCurrentBeat = 0;
            }
            GUI.backgroundColor = prevBg;
        }

        private void DrawMetronomeBeatVisualizer()
        {
            Rect barRect = GUILayoutUtility.GetRect(100, 1000, 24, 24);
            EditorGUI.DrawRect(barRect, new Color(0.09f, 0.10f, 0.13f, 1f));

            int beats = Math.Clamp(_metronomeBeatsPerBar, 1, 8);
            float spacing = 4f;
            float beatWidth = (barRect.width - (beats - 1) * spacing) / beats;
            double hitAge = EditorApplication.timeSinceStartup - _lastMetronomeClickTime;
            bool isFlashing = hitAge < 0.15;

            for (int b = 0; b < beats; b++)
            {
                Rect bRect = new Rect(barRect.x + b * (beatWidth + spacing), barRect.y + 2, beatWidth, barRect.height - 4);
                bool isActive = _metronomeLoopRunning && _metronomeCurrentBeat == ((b + 1) % beats) && isFlashing;
                bool isDownbeat = b == 0;

                Color bColor;
                if (isActive)
                {
                    bColor = isDownbeat ? new Color(0.22f, 0.95f, 0.50f, 0.95f) : new Color(0.25f, 0.85f, 1f, 0.95f);
                }
                else
                {
                    bColor = isDownbeat ? new Color(0.18f, 0.26f, 0.22f, 0.85f) : new Color(0.15f, 0.17f, 0.20f, 0.85f);
                }

                EditorGUI.DrawRect(bRect, bColor);

                var labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = isActive ? Color.black : (isDownbeat ? new Color(0.75f, 0.95f, 0.80f) : new Color(0.75f, 0.82f, 0.90f)) },
                    fontSize = 10
                };
                string bLabel = isDownbeat ? $"{b + 1} (Accent)" : $"{b + 1}";
                GUI.Label(bRect, bLabel, labelStyle);
            }
        }

        private void DrawMetronomeRoutingAndMeterColumn()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Output Routing & Level Meters", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                int targetPair = _metronomeTargetChannel switch
                {
                    3 => 1,
                    5 => 2,
                    7 => 3,
                    _ => 0
                };

                // Channel Strip List (unified route buttons + VU meters)
                DrawChannelPairMeter(0, "Pair 1 [Ch 1-2]", "Front Left + Right (Main FOH)", -1, targetPair == 0);
                EditorGUILayout.Space(3);
                DrawChannelPairMeter(1, "Pair 2 [Ch 3-4]", "Aux / Drummer In-Ear Click", 3, targetPair == 1);
                EditorGUILayout.Space(3);
                DrawChannelPairMeter(2, "Pair 3 [Ch 5-6]", "Center / Subwoofer (LFE)", 5, targetPair == 2);
                EditorGUILayout.Space(3);
                DrawChannelPairMeter(3, "Pair 4 [Ch 7-8]", "Surround Back Left + Right", 7, targetPair == 3);

                EditorGUILayout.Space(8);

                // Headphone Audition Mode Card
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        _headphoneAuditionMode = EditorGUILayout.ToggleLeft(" 🎧 Stereo Headphone Audition Mode", _headphoneAuditionMode, EditorStyles.boldLabel);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Repaint();
                        }
                    }

                    var helpStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        wordWrap = true,
                        normal = { textColor = new Color(0.75f, 0.8f, 0.85f) }
                    };
                    GUILayout.Label("Audition non-default output channels (Ch 3-8) in standard stereo headphones while testing multi-channel routing isolation.", helpStyle);
                }
            }
        }

        private void DrawChannelPairMeter(int pairIndex, string title, string subtitle, int channelId, bool isTarget)
        {
            var prevBg = GUI.backgroundColor;
            if (isTarget)
            {
                GUI.backgroundColor = new Color(0.25f, 0.65f, 0.95f, 1f);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = prevBg;

                // Channel info
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(200)))
                {
                    var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 11,
                        normal = { textColor = isTarget ? new Color(0.35f, 0.85f, 1f) : new Color(0.85f, 0.88f, 0.92f) }
                    };
                    GUILayout.Label(title, titleStyle, GUILayout.Height(16));
                    GUILayout.Label(subtitle, EditorStyles.miniLabel);
                }

                GUILayout.Space(4);

                // Real-time LED VU Meter Bar
                Rect meterRect = GUILayoutUtility.GetRect(100, 1000, 22, 22);
                EditorGUI.DrawRect(meterRect, new Color(0.08f, 0.09f, 0.12f, 1f));

                float minDb = -60f;
                float maxDb = 0f;
                float curDb = _channelPairPeaks[pairIndex];
                float peakHoldDb = _channelPairPeakHold[pairIndex];

                float normCurrent = Mathf.Clamp01((curDb - minDb) / (maxDb - minDb));
                float normPeak = Mathf.Clamp01((peakHoldDb - minDb) / (maxDb - minDb));
                float fillWidth = normCurrent * meterRect.width;

                if (fillWidth > 0)
                {
                    float yellowSplit = (-12f - minDb) / (maxDb - minDb);
                    float redSplit = (-3f - minDb) / (maxDb - minDb);

                    float greenW = Math.Min(fillWidth, yellowSplit * meterRect.width);
                    if (greenW > 0)
                    {
                        EditorGUI.DrawRect(new Rect(meterRect.x, meterRect.y + 1, greenW, meterRect.height - 2), new Color(0.2f, 0.85f, 0.45f, 0.9f));
                    }

                    if (fillWidth > yellowSplit * meterRect.width)
                    {
                        float yStart = meterRect.x + (yellowSplit * meterRect.width);
                        float yW = Math.Min(fillWidth - (yellowSplit * meterRect.width), (redSplit - yellowSplit) * meterRect.width);
                        EditorGUI.DrawRect(new Rect(yStart, meterRect.y + 1, yW, meterRect.height - 2), new Color(0.95f, 0.75f, 0.25f, 0.9f));
                    }

                    if (fillWidth > redSplit * meterRect.width)
                    {
                        float rStart = meterRect.x + (redSplit * meterRect.width);
                        float rW = fillWidth - (redSplit * meterRect.width);
                        EditorGUI.DrawRect(new Rect(rStart, meterRect.y + 1, rW, meterRect.height - 2), new Color(1f, 0.32f, 0.32f, 0.95f));
                    }
                }

                if (normPeak > 0)
                {
                    float pX = meterRect.x + (normPeak * meterRect.width);
                    EditorGUI.DrawRect(new Rect(pX - 1, meterRect.y, 2, meterRect.height), new Color(1f, 1f, 1f, 0.9f));
                }

                string dbText = curDb > -80f ? $"{curDb:F1} dB" : "-inf dB";
                var textStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.95f, 0.95f, 0.95f, 0.9f) },
                    fontSize = 9
                };
                GUI.Label(meterRect, dbText, textStyle);

                GUILayout.Space(6);

                // Active Badge / Route Here Button
                if (!isTarget)
                {
                    if (GUILayout.Button("Route Here", EditorStyles.miniButton, GUILayout.Width(80), GUILayout.Height(20)))
                    {
                        SetMetronomeTargetChannel(channelId);
                        TriggerMetronomeClick(true);
                    }
                }
                else
                {
                    var activeBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.2f, 0.85f, 0.45f, 1f);
                    GUILayout.Label("● ACTIVE", EditorStyles.miniButton, GUILayout.Width(80), GUILayout.Height(20));
                    GUI.backgroundColor = activeBg;
                }
            }
        }

        #endregion
    }
}

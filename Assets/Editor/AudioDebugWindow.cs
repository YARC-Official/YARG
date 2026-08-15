#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private const int DEFAULT_BUFFER_MS = 100;
        private const string RECENT_PATHS_KEY = "YARG_AudioDebug_RecentPaths";
        private const int MAX_RECENT_PATHS = 8;
        private const float MIN_WINDOW_WIDTH = 740f;
        private const float MIN_WINDOW_HEIGHT = 780f;
        private const float DEFAULT_WINDOW_WIDTH = 900f;
        private const float DEFAULT_WINDOW_HEIGHT = 840f;

        private enum GraphMode
        {
            PositionJitter,
            SyncConvergence,
            FrameStepDelta,
            PositionMappingStep,
            CallbackTimingStep,
            ControlHeardDelta,
            AbsolutePosition,
            MicPitchAndHits,
            FrequencySpectrum
        }

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

        private GraphMode _graphMode = GraphMode.PositionJitter;
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
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            DisposeSong();
            DisconnectMicrophone();
        }

        private void OnDestroy()
        {
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
            UpdateFft(now, dt);
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
                _lastMicSampleTime = now;
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

            if (_modelSongSync && _audioSynchronizer != null)
            {
                _audioSynchronizer.Synchronize(controlTargetTime, heardTargetTime, _playbackSpeed,
                    currentInputSystemTime);
            }

            if (now - _lastSampleTime >= SAMPLE_INTERVAL)
            {
                _lastSampleTime = now;

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

            if (_graphMode == GraphMode.FrequencySpectrum || _selectedBottomTab == 4)
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
                        GUI.backgroundColor = new Color(0.15f, 0.75f, 0.35f, 1f);
                        GUILayout.Label(" ● PLAYING ", EditorStyles.helpBox, GUILayout.Height(22));
                    }
                    else if (isLoaded)
                    {
                        GUI.backgroundColor = new Color(0.95f, 0.65f, 0.15f, 1f);
                        GUILayout.Label(" ⏸ PAUSED ", EditorStyles.helpBox, GUILayout.Height(22));
                    }
                    else
                    {
                        GUI.backgroundColor = new Color(0.40f, 0.45f, 0.52f, 1f);
                        GUILayout.Label(" ⏹ STOPPED ", EditorStyles.helpBox, GUILayout.Height(22));
                    }
                    GUI.backgroundColor = prevBg;

                    GUILayout.Space(6);

                    var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 14,
                        alignment = TextAnchor.MiddleLeft
                    };
                    EditorGUILayout.LabelField(_loadedSongName, titleStyle, GUILayout.Height(22));

                    GUILayout.FlexibleSpace();

                    string currentDevice = SettingsManager.Settings?.OutputDevice.Value ?? "Default";
                    var currentMode = GlobalAudioHandler.GetOutputMode(currentDevice);
                    string cleanDev = CleanDeviceName(currentDevice);
                    string devButtonLabel = currentMode == AudioOutputMode.Asio ? $"⚡ ASIO: {cleanDev} ▾" : $"🔊 {cleanDev} ▾";

                    if (GUILayout.Button(devButtonLabel, EditorStyles.miniButton, GUILayout.Height(22), GUILayout.MaxWidth(230)))
                    {
                        ShowDeviceMenu();
                    }

                    GUILayout.Space(4);

                    if (GUILayout.Button("Open Audio ▾", EditorStyles.miniButton, GUILayout.Height(22), GUILayout.Width(95)))
                    {
                        ShowAudioMenu();
                    }

                    GUI.enabled = !string.IsNullOrEmpty(_sourcePath);
                    if (GUILayout.Button("Reveal", EditorStyles.miniButton, GUILayout.Height(22), GUILayout.Width(55)))
                    {
                        EditorUtility.RevealInFinder(_sourcePath);
                    }
                    GUI.enabled = true;
                }

                EditorGUILayout.Space(3);

                int sampleRate = Bass.Info.SampleRate;
                int speakers = Bass.Info.SpeakerCount;
                string activeDevice = SettingsManager.Settings?.OutputDevice.Value ?? "Default";
                string cleanActive = CleanDeviceName(activeDevice);
                var mode = GlobalAudioHandler.GetOutputMode(activeDevice);
                string modeLabel = mode == AudioOutputMode.Asio ? "ASIO" : "Shared";
                double latencyMs = GlobalAudioHandler.PlaybackLatency;
                var bufferInfo = GlobalAudioHandler.GetOutputBufferInfo();
                string bufferStr = bufferInfo is { } bInfo && bInfo.PreferredLength > 0 ? $" • {bInfo.PreferredLength} spl" : string.Empty;

                string pathText = string.IsNullOrEmpty(_sourcePath) ? "Drag & drop audio file or folder to load" : _sourcePath;
                string metaText = $"{pathText}   •   {cleanActive} [{modeLabel}] ({sampleRate} Hz, {speakers} ch, {latencyMs:F1} ms latency{bufferStr})";

                EditorGUILayout.LabelField(metaText, EditorStyles.miniLabel);
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

                    if (GUILayout.Button("Scan Songs", GUILayout.Width(85), GUILayout.Height(19)))
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
                        if (GUILayout.Button("Load", GUILayout.Width(55), GUILayout.Height(18)))
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
                        if (GUILayout.Button("⏸ Pause", GUILayout.Width(85), GUILayout.Height(28)))
                        {
                            PauseSong();
                        }
                    }
                    else
                    {
                        GUI.backgroundColor = isLoaded ? new Color(0.2f, 0.78f, 0.35f, 1f) : prevBg;
                        if (GUILayout.Button("▶ Play", GUILayout.Width(85), GUILayout.Height(28)))
                        {
                            PlaySong();
                        }
                    }
                    GUI.backgroundColor = prevBg;

                    if (GUILayout.Button("⏹ Stop", GUILayout.Width(60), GUILayout.Height(28)))
                    {
                        StopSong();
                    }

                    GUILayout.Space(4);

                    if (GUILayout.Button("-5s", EditorStyles.miniButtonLeft, GUILayout.Width(36), GUILayout.Height(28))) JumpRelative(-5.0);
                    if (GUILayout.Button("-1s", EditorStyles.miniButtonMid, GUILayout.Width(36), GUILayout.Height(28))) JumpRelative(-1.0);
                    if (GUILayout.Button("+1s", EditorStyles.miniButtonMid, GUILayout.Width(36), GUILayout.Height(28))) JumpRelative(1.0);
                    if (GUILayout.Button("+5s", EditorStyles.miniButtonRight, GUILayout.Width(36), GUILayout.Height(28))) JumpRelative(5.0);

                    GUILayout.Space(6);

                    var timeStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 12,
                        alignment = TextAnchor.MiddleCenter
                    };

                    EditorGUILayout.LabelField($"{FormatTime(currentPos)} / {FormatTime(totalLength)}", timeStyle, GUILayout.Width(130), GUILayout.Height(28));

                    GUILayout.Space(4);

                    GUI.enabled = isLoaded && totalLength > 0;
                    float displayPos = _isScrubbing ? _scrubTarget : (float) currentPos;
                    EditorGUI.BeginChangeCheck();
                    float newPos = GUILayout.HorizontalSlider(displayPos, 0f, Mathf.Max(0.1f, (float) totalLength), GUILayout.Height(28));
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

                EditorGUILayout.Space(3);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Speed:", GUILayout.Width(45));
                    DrawSpeedPill(0.5f);
                    DrawSpeedPill(0.75f);
                    DrawSpeedPill(1.0f);
                    DrawSpeedPill(1.25f);
                    DrawSpeedPill(1.5f);

                    float newSpeed = EditorGUILayout.Slider(_playbackSpeed, 0.1f, 2.5f, GUILayout.Width(95));
                    if (Mathf.Abs(newSpeed - _playbackSpeed) > 0.001f)
                    {
                        SetPlaybackSpeed(newSpeed);
                    }

                    if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(45)))
                    {
                        SetPlaybackSpeed(1f);
                    }

                    GUILayout.FlexibleSpace();

                    EditorGUILayout.LabelField("Volume:", GUILayout.Width(50));
                    float newVol = EditorGUILayout.Slider(_volume, 0f, 1f, GUILayout.Width(110));
                    if (Mathf.Abs(newVol - _volume) > 0.001f)
                    {
                        _volume = newVol;
                        _bassSong?.SetVolume(_volume);
                    }
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

        private void DrawSpeedPill(float speed)
        {
            bool isActive = Mathf.Approximately(_playbackSpeed, speed);
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button($"{speed:0.##}x", EditorStyles.miniButton, GUILayout.Width(40), GUILayout.Height(18)))
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
                    EditorGUILayout.LabelField("Real-Time Oscilloscope & Jitter", EditorStyles.boldLabel, GUILayout.Width(200));

                    GUILayout.FlexibleSpace();

                    _graphMode = (GraphMode) EditorGUILayout.EnumPopup(_graphMode, GUILayout.Width(145));

                    if (_graphMode == GraphMode.FrequencySpectrum)
                    {
                        GUILayout.Space(6);
                        EditorGUILayout.LabelField("FFT:", GUILayout.Width(28));
                        DrawFftSizePill(8, "256");
                        DrawFftSizePill(9, "512");
                        DrawFftSizePill(10, "1k");
                        DrawFftSizePill(11, "2k");
                        DrawFftSizePill(12, "4k");

                        GUILayout.Space(6);
                        DrawFftStylePill(FftDisplayStyle.FilledCurve, "Curve");
                        DrawFftStylePill(FftDisplayStyle.RtaBars, "Bars");
                        DrawFftStylePill(FftDisplayStyle.Both, "Both");

                        GUILayout.Space(6);
                        DrawFftScalePill(FftScaleMode.Logarithmic, "Log");
                        DrawFftScalePill(FftScaleMode.Linear, "Lin");
                    }
                    else if (_graphMode == GraphMode.MicPitchAndHits)
                    {
                        GUILayout.Space(6);
                        EditorGUILayout.LabelField("Win:", GUILayout.Width(28));
                        DrawWindowPill(1f, "1s");
                        DrawWindowPill(2f, "2s");
                        DrawWindowPill(3f, "3s");
                        DrawWindowPill(5f, "5s");
                        DrawWindowPill(10f, "10s");

                        GUILayout.Space(6);
                        EditorGUILayout.LabelField("Range: C2–C6", EditorStyles.miniLabel, GUILayout.Width(85));
                    }
                    else
                    {
                        GUILayout.Space(6);
                        EditorGUILayout.LabelField("Win:", GUILayout.Width(28));
                        DrawWindowPill(1f, "1s");
                        DrawWindowPill(2f, "2s");
                        DrawWindowPill(3f, "3s");
                        DrawWindowPill(5f, "5s");
                        DrawWindowPill(10f, "10s");

                        if (_graphMode != GraphMode.AbsolutePosition)
                        {
                            GUILayout.Space(6);
                            EditorGUILayout.LabelField("Y:", GUILayout.Width(16));
                            DrawYScalePill(0f, "Auto");
                            DrawYScalePill(5f, "±5");
                            DrawYScalePill(10f, "±10");
                            DrawYScalePill(25f, "±25");
                        }
                    }

                    GUILayout.Space(6);

                    var prevBg = GUI.backgroundColor;
                    if (_autoScroll)
                    {
                        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f, 1f);
                    }
                    if (GUILayout.Button(_autoScroll ? "● Live" : "Live", EditorStyles.miniButton, GUILayout.Width(48)))
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
                    if (GUILayout.Button("Freeze", EditorStyles.miniButton, GUILayout.Width(48)))
                    {
                        _freezeGraph = !_freezeGraph;
                    }
                    GUI.backgroundColor = prevBg;

                    if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(42)))
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

                Rect graphRect = GUILayoutUtility.GetRect(100, 1000, 210, 360);
                DrawGraphArea(graphRect);

                EditorGUILayout.Space(3);
                DrawGraphTimelineMiniBar();

                EditorGUILayout.Space(4);
                DrawGraphHudRibbon();
            }
        }

        private void DrawWindowPill(float windowSeconds, string label)
        {
            bool isActive = Mathf.Approximately(_graphTimeWindow, windowSeconds);
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(28), GUILayout.Height(18)))
            {
                _graphTimeWindow = windowSeconds;
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawYScalePill(float scaleMs, string label)
        {
            bool isActive = Mathf.Approximately(_jitterScaleMs, scaleMs);
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(34), GUILayout.Height(18)))
            {
                _jitterScaleMs = scaleMs;
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawFftSizePill(int logSize, string label)
        {
            bool isActive = _fftSizeLog == logSize;
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(32), GUILayout.Height(18)))
            {
                _fftSizeLog = logSize;
                _fftBuffer = null;
                _smoothedFft = null;
                _peakFft = null;
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawFftStylePill(FftDisplayStyle style, string label)
        {
            bool isActive = _fftDisplayStyle == style;
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(42), GUILayout.Height(18)))
            {
                _fftDisplayStyle = style;
            }

            GUI.backgroundColor = prevBg;
        }

        private void DrawFftScalePill(FftScaleMode mode, string label)
        {
            bool isActive = _fftScaleMode == mode;
            var prevBg = GUI.backgroundColor;
            if (isActive)
            {
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f, 1f);
            }

            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(34), GUILayout.Height(18)))
            {
                _fftScaleMode = mode;
            }

            GUI.backgroundColor = prevBg;
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
                    double meanTime = windowSamples.Average(s => s.RealTime);
                    double meanHeard = windowSamples.Average(s => s.HeardPosition);
                    double meanControl = windowSamples.Average(s => s.ControlPosition);

                    double numHeard = 0;
                    double numControl = 0;
                    double denom = 0;

                    for (int i = 0; i < windowSamples.Count; i++)
                    {
                        double dt = windowSamples[i].RealTime - meanTime;
                        numHeard += dt * (windowSamples[i].HeardPosition - meanHeard);
                        numControl += dt * (windowSamples[i].ControlPosition - meanControl);
                        denom += dt * dt;
                    }

                    double slopeHeard = denom > 0.00001 ? numHeard / denom : 1.0;
                    double slopeControl = denom > 0.00001 ? numControl / denom : 1.0;

                    double interceptHeard = meanHeard - (slopeHeard * meanTime);
                    double interceptControl = meanControl - (slopeControl * meanTime);

                    for (int i = 0; i < windowSamples.Count; i++)
                    {
                        double expectedHeard = interceptHeard + (slopeHeard * windowSamples[i].RealTime);
                        double residualHeardMs = (windowSamples[i].HeardPosition - expectedHeard) * 1000.0;
                        heardValues.Add(residualHeardMs);

                        double expectedControl = interceptControl + (slopeControl * windowSamples[i].RealTime);
                        double residualControlMs = (windowSamples[i].ControlPosition - expectedControl) * 1000.0;
                        controlValues.Add(residualControlMs);
                    }
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
            else if (_graphMode == GraphMode.ControlHeardDelta)
            {
                if (_jitterScaleMs > 0)
                {
                    minY = -_jitterScaleMs;
                    maxY = _jitterScaleMs;
                }
            }

            if (minY >= maxY)
            {
                minY = -1.0;
                maxY = 1.0;
            }

            double yRange = maxY - minY;

            DrawGrid(rect, minTime, maxTime, minY, maxY, _graphMode);

            if (_graphMode == GraphMode.PositionJitter || _graphMode == GraphMode.ControlHeardDelta || _graphMode == GraphMode.SyncConvergence)
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
                GraphMode.PositionJitter => $"Time: {sample.RealTime:F2}s\nHeard: {heardVal:+0.00;-0.00;0.00} ms\nCtrl: {controlVal:+0.00;-0.00;0.00} ms",
                GraphMode.SyncConvergence => $"Time: {sample.RealTime:F2}s\nHeard Err: {sample.HeardErrorMs:+0.00;-0.00;0.00} ms\nCtrl Err: {sample.ControlErrorMs:+0.00;-0.00;0.00} ms\nState: {sample.SyncState} ({sample.Adjustment * 100:+0.00;-0.00;0.00}%)",
                GraphMode.FrameStepDelta => $"Time: {sample.RealTime:F2}s\nHeard Step: {heardVal:F2} ms\nCtrl Step: {controlVal:F2} ms",
                GraphMode.PositionMappingStep => $"Time: {sample.RealTime:F2}s\nHeard Step: {heardVal:F2} ms\nOutput Step: {controlVal:F2} ms",
                GraphMode.CallbackTimingStep => $"Time: {sample.RealTime:F2}s\nCallback: {heardVal:F2} ms\nElapsed: {controlVal:F2} ms\nCorrection: {sample.CallbackCorrectionMs:+0.00;-0.00;0.00} ms\nClock Offset: {sample.CallbackClockOffsetMs:+0.00;-0.00;0.00} ms",
                GraphMode.ControlHeardDelta => $"Time: {sample.RealTime:F2}s\nDelta: {controlVal:+0.00;-0.00;0.00} ms",
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
            if (_graphMode == GraphMode.FrequencySpectrum)
            {
                DrawFftTimelineMiniBar();
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

            if (_graphMode == GraphMode.FrequencySpectrum)
            {
                DrawFftHudRibbon();
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
                    double meanT = window.Average(s => s.RealTime);
                    double meanP = window.Average(s => s.HeardPosition);
                    double num = 0, den = 0;
                    foreach (var s in window)
                    {
                        double dt = s.RealTime - meanT;
                        num += dt * (s.HeardPosition - meanP);
                        den += dt * dt;
                    }
                    double slope = den > 0.00001 ? num / den : 1.0;
                    double intercept = meanP - (slope * meanT);

                    var residuals = window.Select(s => (s.HeardPosition - (intercept + (slope * s.RealTime))) * 1000.0).ToList();
                    peakToPeakJitter = residuals.Max() - residuals.Min();
                    double meanRes = residuals.Average();
                    stdDevJitter = Math.Sqrt(residuals.Average(r => (r - meanRes) * (r - meanRes)));
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (_graphMode == GraphMode.SyncConvergence)
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

                GUILayout.Space(6);

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(80), GUILayout.Height(36)))
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Copy Stats", EditorStyles.miniButton, GUILayout.Width(80), GUILayout.Height(22)))
                    {
                        bool anyReverb = _stemReverbs.Values.Any(r => r);
                        GUIUtility.systemCopyBuffer = $"Mode: {_graphMode} | Buffer: {_readAheadBufferMs}ms | Speed: {_playbackSpeed:0.##}x | Reverb: {(anyReverb ? "ON" : "OFF")}\n" +
                                                      $"Jitter (P-P): {peakToPeakJitter:F2} ms | StdDev: {stdDevJitter:F2} ms | Delta: {deltaMs:F2} ms | FPS: {_currentFps:F0}";
                    }
                    GUILayout.FlexibleSpace();
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
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(80), GUILayout.Height(36)))
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Reset Hits", EditorStyles.miniButton, GUILayout.Width(80), GUILayout.Height(22)))
                    {
                        _totalHitCount = 0;
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private static void DrawMetricTile(string title, string value, Color valueColor)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.Height(36)))
            {
                var titleStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 9,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = new Color(0.7f, 0.75f, 0.82f) }
                };
                GUILayout.Label(title, titleStyle, GUILayout.Height(12));

                var valStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    alignment = TextAnchor.LowerLeft,
                    normal = { textColor = valueColor }
                };
                GUILayout.Label(value, valStyle, GUILayout.Height(18));
            }
        }

        private void DrawBottomDashboard()
        {
            int channelCount = _bassSong?.Channels?.Count ?? 0;
            string mixerTabLabel = channelCount > 0 ? $"Stem Mixer ({channelCount})" : "Stem Mixer";
            string micTabLabel = _activeMicDevice != null ? "🎤 Input (Active)" : "🎤 Microphone & Input";

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string[] tabLabels = { $"🎚️ {mixerTabLabel}", "⏱️ Sync & Simulation", "🔊 Output & Engine Health", micTabLabel, "📊 FFT & Spectrum" };
                    int newTab = GUILayout.Toolbar(_selectedBottomTab, tabLabels, GUILayout.Height(24));
                    if (newTab != _selectedBottomTab)
                    {
                        _selectedBottomTab = newTab;
                        _graphMode = _selectedBottomTab switch
                        {
                            0 => GraphMode.SyncConvergence,
                            1 => GraphMode.SyncConvergence,
                            2 => GraphMode.PositionJitter,
                            3 => GraphMode.MicPitchAndHits,
                            4 => GraphMode.FrequencySpectrum,
                            _ => _graphMode
                        };
                    }
                }

                EditorGUILayout.Space(6);

                switch (_selectedBottomTab)
                {
                    case 0:
                        DrawStemMixerCard();
                        break;
                    case 1:
                        DrawSongSyncCard();
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
                        DrawFftDashboardCard();
                        break;
                }
            }
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

                if (GUILayout.Button("Select Device ▾", EditorStyles.toolbarDropDown, GUILayout.Width(110)))
                {
                    ShowMicDeviceMenu();
                }

                if (isConnected)
                {
                    if (GUILayout.Button("Disconnect", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    {
                        DisconnectMicrophone();
                    }
                }
                else if (_selectedMicDevice.HasValue)
                {
                    if (GUILayout.Button("Connect", EditorStyles.toolbarButton, GUILayout.Width(70)))
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
                    if (GUILayout.Button("Reset Stream", EditorStyles.miniButton, GUILayout.Width(95), GUILayout.Height(18)))
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

                    if (GUILayout.Button("Switch Device ▾", EditorStyles.miniButton, GUILayout.Width(110), GUILayout.Height(18)))
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
                    _modelSongSync = EditorGUILayout.ToggleLeft("Model Song Sync (Input Time)", _modelSongSync, EditorStyles.boldLabel, GUILayout.Width(220));

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

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Calibration:", GUILayout.Width(75));
                    float newCal = EditorGUILayout.Slider(_audioCalibrationMs, -200f, 200f);
                    if (Mathf.Abs(newCal - _audioCalibrationMs) > 0.01f)
                    {
                        _audioCalibrationMs = newCal;
                        _bassSong?.SetOutputLatency(_audioCalibrationMs / 1000.0);
                    }

                    if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(45)))
                    {
                        _audioCalibrationMs = 0f;
                        _bassSong?.SetOutputLatency(0.0);
                    }
                }

                if (_bassSong != null)
                {
                    var syncPos = _bassSong.GetSyncPosition();
                    double currentInputSystemTime = InputManager.CurrentInputTime;
                    double currentInputTime = (currentInputSystemTime - _inputTimeOffset + _simulatedClockDisturbance) * _playbackSpeed;
                    double controlTargetTime = _bassSong.IsPaused ? _bassSong.GetPosition() : currentInputTime;
                    double audioCalibrationSeconds = _audioCalibrationMs / 1000.0;
                    double heardTargetTime = controlTargetTime + (audioCalibrationSeconds * _playbackSpeed);

                    double heardErr = _samples.Count > 0 && !_bassSong.IsPaused
                        ? _samples[_samples.Count - 1].HeardErrorMs
                        : (heardTargetTime - syncPos.Heard) * 1000.0;

                    double ctrlErr = _samples.Count > 0 && !_bassSong.IsPaused
                        ? _samples[_samples.Count - 1].ControlErrorMs
                        : (_audioSynchronizer != null && _modelSongSync ? _audioSynchronizer.ControlError * 1000.0 : (controlTargetTime - syncPos.Control) * 1000.0);

                    float worstDelta = _audioSynchronizer?.WorstDelta * 1000f ?? 0f;
                    float effectiveSpeed = _playbackSpeed + (_audioSynchronizer?.EffectiveAdjustment ?? 0f);

                    EditorGUILayout.Space(2);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Heard Err: {heardErr:+0.00;-0.00;0.00} ms", EditorStyles.miniLabel, GUILayout.Width(130));
                        EditorGUILayout.LabelField($"Ctrl Err: {ctrlErr:+0.00;-0.00;0.00} ms", EditorStyles.miniLabel, GUILayout.Width(130));
                        EditorGUILayout.LabelField($"Worst Δ: {worstDelta:+0.0;-0.0;0.0} ms", EditorStyles.miniLabel, GUILayout.Width(110));
                        EditorGUILayout.LabelField($"Rate: {effectiveSpeed:F3}x", EditorStyles.miniBoldLabel);
                    }

                    EditorGUILayout.Space(4);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Step Jump:", GUILayout.Width(70));
                        if (GUILayout.Button("-100ms", EditorStyles.miniButtonLeft)) _simulatedClockDisturbance -= 0.100;
                        if (GUILayout.Button("-50ms", EditorStyles.miniButtonMid)) _simulatedClockDisturbance -= 0.050;
                        if (GUILayout.Button("-20ms", EditorStyles.miniButtonMid)) _simulatedClockDisturbance -= 0.020;
                        if (GUILayout.Button("+20ms", EditorStyles.miniButtonMid)) _simulatedClockDisturbance += 0.020;
                        if (GUILayout.Button("+50ms", EditorStyles.miniButtonMid)) _simulatedClockDisturbance += 0.050;
                        if (GUILayout.Button("+100ms", EditorStyles.miniButtonRight)) _simulatedClockDisturbance += 0.100;

                        if (GUILayout.Button("0ms", EditorStyles.miniButton, GUILayout.Width(35)))
                        {
                            _simulatedClockDisturbance = 0;
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Clock Drift:", GUILayout.Width(70));
                        _simulatedClockDriftPercent = EditorGUILayout.Slider(_simulatedClockDriftPercent, -2.0f, 2.0f);
                        if (GUILayout.Button("0%", EditorStyles.miniButton, GUILayout.Width(35)))
                        {
                            _simulatedClockDriftPercent = 0f;
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Stress Test:", GUILayout.Width(70));
                        if (GUILayout.Button("Trigger GC Collect", EditorStyles.miniButton))
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
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
                        if (GUILayout.Button(anyReverb ? "★ All Reverb ON" : "☆ All Reverb OFF", EditorStyles.miniButton, GUILayout.Width(110)))
                        {
                            ToggleAllReverb(!anyReverb);
                        }
                        GUI.backgroundColor = prevBg;

                        if (GUILayout.Button("Reset All", EditorStyles.miniButton, GUILayout.Width(65)))
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

            if (GUILayout.Button($"{ms}ms", EditorStyles.miniButton, GUILayout.Height(18)))
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

        private void DrawFftDashboardCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawFftTopStatusBar();

                EditorGUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawFftBandsStageColumn();

                    GUILayout.Space(6);

                    DrawFftAcousticsColumn();

                    GUILayout.Space(6);

                    DrawFftControlsColumn();
                }

                EditorGUILayout.Space(6);

                Rect graphRect = GUILayoutUtility.GetRect(100, 10000, 180, 240, GUILayout.ExpandWidth(true));
                if (graphRect.width > 50 && graphRect.height > 50)
                {
                    float paddingLeft = 52f;
                    float paddingBottom = 20f;
                    float paddingTop = 10f;
                    float paddingRight = 10f;

                    float plotWidth = graphRect.width - paddingLeft - paddingRight;
                    float plotHeight = graphRect.height - paddingTop - paddingBottom;
                    var plotRect = new Rect(graphRect.x + paddingLeft, graphRect.y + paddingTop, plotWidth, plotHeight);

                    DrawFftSpectrumGraph(graphRect, plotRect, paddingLeft, paddingTop, paddingRight, paddingBottom, plotWidth, plotHeight);
                }
            }
        }

        private void DrawFftTopStatusBar()
        {
            bool isPlaying = _bassSong != null && !_bassSong.IsPaused;
            int sampleRate = Bass.Info.SampleRate > 0 ? Bass.Info.SampleRate : 44100;
            int fftPoints = 1 << _fftSizeLog;
            int binCount = fftPoints / 2;
            float binResolution = (sampleRate * 0.5f) / binCount;

            using (new EditorGUILayout.HorizontalScope())
            {
                var dotColor = isPlaying ? new Color(0.2f, 0.9f, 0.4f) : Color.gray;
                var dotStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = dotColor }
                };
                GUILayout.Label(isPlaying ? "● LIVE FFT SPECTRUM" : "○ FFT IDLE", dotStyle, GUILayout.Width(160));

                GUILayout.FlexibleSpace();

                var metaStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.7f, 0.75f, 0.85f) }
                };
                string songState = _bassSong == null ? "No Song Loaded" : (_bassSong.IsPaused ? "Paused" : "Playing");
                GUILayout.Label($"State: {songState}  |  Read: {_lastFftBytesRead} B  |  Sample Rate: {sampleRate / 1000f:0.1} kHz  |  Bins: {binCount} ({binResolution:0.1} Hz/bin)  |  Scale: {_fftScaleMode}", metaStyle);
            }
        }

        private void DrawFftBandsStageColumn()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(270)))
            {
                EditorGUILayout.LabelField("7-Band Acoustic Level Meters", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                for (int b = 0; b < _fftBands.Length; b++)
                {
                    var band = _fftBands[b];
                    float normCur = Mathf.Clamp01((band.CurrentDb - _fftMinDb) / (_fftMaxDb - _fftMinDb));
                    float normPeak = Mathf.Clamp01((band.PeakDb - _fftMinDb) / (_fftMaxDb - _fftMinDb));

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var nameStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            normal = { textColor = band.BandColor },
                            fontStyle = FontStyle.Bold
                        };
                        GUILayout.Label(band.Name, nameStyle, GUILayout.Width(62));

                        Rect meterRect = GUILayoutUtility.GetRect(80, 140, 14, 14);
                        EditorGUI.DrawRect(meterRect, new Color(0.10f, 0.11f, 0.14f, 1f));

                        if (normCur > 0.01f)
                        {
                            float fillW = normCur * meterRect.width;
                            EditorGUI.DrawRect(new Rect(meterRect.x, meterRect.y + 1, fillW, meterRect.height - 2), band.BandColor * 0.85f);
                        }

                        if (normPeak > 0.01f)
                        {
                            float peakX = meterRect.x + (normPeak * meterRect.width);
                            EditorGUI.DrawRect(new Rect(peakX - 1, meterRect.y, 2, meterRect.height), new Color(1f, 0.85f, 0.3f, 1f));
                        }

                        string dbStr = band.CurrentDb > -140f ? $"{band.CurrentDb:F1} dB" : "-∞ dB";
                        var dbStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            alignment = TextAnchor.MiddleRight,
                            normal = { textColor = new Color(0.8f, 0.85f, 0.9f) }
                        };
                        GUILayout.Label(dbStr, dbStyle, GUILayout.Width(52));
                    }
                    EditorGUILayout.Space(2);
                }
            }
        }

        private void DrawFftAcousticsColumn()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Spectral Pitch & Acoustics", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(110), GUILayout.Height(72)))
                    {
                        var pitchTitleStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = new Color(0.65f, 0.7f, 0.8f) }
                        };
                        GUILayout.Label("DOMINANT NOTE", pitchTitleStyle);

                        var bigNoteStyle = new GUIStyle(EditorStyles.boldLabel)
                        {
                            fontSize = 22,
                            alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = _dominantFrequencyHz >= 20f ? new Color(0.2f, 0.95f, 1f) : Color.gray }
                        };
                        GUILayout.Label(_dominantNoteName, bigNoteStyle);

                        string hzLabel = _dominantFrequencyHz >= 20f ? $"{_dominantFrequencyHz:F1} Hz" : "-- Hz";
                        var hzStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            normal = { textColor = new Color(0.8f, 0.85f, 0.9f) }
                        };
                        GUILayout.Label(hzLabel, hzStyle);
                    }

                    GUILayout.Space(6);

                    using (new EditorGUILayout.VerticalScope())
                    {
                        string timbreDesc = _spectralCentroidHz switch
                        {
                            < 250f => "Deep / Sub-Heavy",
                            < 600f => "Warm / Bass-Rich",
                            < 1500f => "Full / Balanced Low-Mid",
                            < 3500f => "Clear / Mid-Forward",
                            < 6000f => "Bright / Present",
                            _ => "Crisp / Airy Highs"
                        };

                        EditorGUILayout.LabelField($"Spectral Centroid: {_spectralCentroidHz:F0} Hz", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"Perceived Timbre: {timbreDesc}", EditorStyles.miniLabel);

                        EditorGUILayout.Space(4);

                        EditorGUILayout.LabelField($"Pitch Offset: {_dominantCents:+0.0;-0.0;0.0} cents", EditorStyles.miniLabel);
                        Rect centsRect = GUILayoutUtility.GetRect(100, 200, 10, 10);
                        EditorGUI.DrawRect(centsRect, new Color(0.12f, 0.13f, 0.16f, 1f));
                        EditorGUI.DrawRect(new Rect(centsRect.x + (centsRect.width * 0.5f) - 1, centsRect.y, 2, centsRect.height), new Color(0.4f, 0.45f, 0.55f, 1f));

                        if (_dominantFrequencyHz >= 20f)
                        {
                            float normCents = Mathf.Clamp(_dominantCents / 50f, -1f, 1f);
                            float midX = centsRect.x + (centsRect.width * 0.5f);
                            float barX = normCents >= 0 ? midX : midX + (normCents * (centsRect.width * 0.5f));
                            float barW = Mathf.Abs(normCents) * (centsRect.width * 0.5f);
                            Color centsColor = MathF.Abs(_dominantCents) < 10f ? new Color(0.25f, 0.95f, 0.45f) : new Color(1f, 0.65f, 0.15f);
                            EditorGUI.DrawRect(new Rect(barX, centsRect.y + 1, Mathf.Max(2f, barW), centsRect.height - 2), centsColor);
                        }
                    }
                }
            }
        }

        private void DrawFftControlsColumn()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(260)))
            {
                EditorGUILayout.LabelField("FFT & Visualizer Configuration", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Resolution:", GUILayout.Width(75));
                    DrawFftSizePill(8, "256");
                    DrawFftSizePill(9, "512");
                    DrawFftSizePill(10, "1k");
                    DrawFftSizePill(11, "2k");
                    DrawFftSizePill(12, "4k");
                }

                EditorGUILayout.Space(2);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Style:", GUILayout.Width(75));
                    DrawFftStylePill(FftDisplayStyle.FilledCurve, "Curve");
                    DrawFftStylePill(FftDisplayStyle.RtaBars, "Bars");
                    DrawFftStylePill(FftDisplayStyle.Both, "Both");
                }

                EditorGUILayout.Space(2);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Freq Scale:", GUILayout.Width(75));
                    DrawFftScalePill(FftScaleMode.Logarithmic, "Log");
                    DrawFftScalePill(FftScaleMode.Linear, "Linear");
                }

                EditorGUILayout.Space(2);

                _fftSmoothingFactor = EditorGUILayout.Slider("Smoothing", _fftSmoothingFactor, 0f, 0.95f);
                _fftMinDb = EditorGUILayout.Slider("Floor (dB)", _fftMinDb, -120f, -40f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _fftPeakHoldEnabled = EditorGUILayout.ToggleLeft("Peak Hold", _fftPeakHoldEnabled, GUILayout.Width(90));
                    if (_fftPeakHoldEnabled)
                    {
                        _fftPeakDecayRate = EditorGUILayout.Slider(_fftPeakDecayRate, 5f, 60f);
                    }
                }
            }
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
    }
}

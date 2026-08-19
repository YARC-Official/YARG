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
    public sealed partial class AudioDebugWindow
    {
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
        private readonly List<PositionSample> _samples = new(MAX_SAMPLES);
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

        // Multi-Microphone Studio Rack
        private readonly List<MicSlot> _micSlots = new();
        private int _selectedMicSlotIndex;
        private static readonly Color[] MIC_SLOT_COLORS =
        {
            new Color(0.15f, 0.85f, 1f, 1f),   // Cyan
            new Color(1f, 0.65f, 0.15f, 1f),   // Orange
            new Color(0.25f, 0.95f, 0.5f, 1f),  // Emerald
            new Color(0.85f, 0.45f, 1f, 1f),   // Violet
            new Color(1f, 0.35f, 0.6f, 1f),    // Rose
            new Color(1f, 0.9f, 0.25f, 1f),    // Gold
            new Color(0.35f, 0.65f, 1f, 1f),   // Sky
            new Color(0.95f, 0.4f, 0.4f, 1f)    // Coral
        };

        private MicSlot? ActiveMicSlot => _micSlots.Count > 0 && _selectedMicSlotIndex >= 0 && _selectedMicSlotIndex < _micSlots.Count ? _micSlots[_selectedMicSlotIndex] : null;
        private List<MicSample> _micSamples => ActiveMicSlot?.Samples ?? _fallbackMicSamples;
        private readonly List<MicSample> _fallbackMicSamples = new();

        public AudioDebugWindow()
        {
        }

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
        private bool _bassBufferSettingsInitialized;
        private int _bassUpdatePeriodMs;
        private int _bassDeviceBufferLengthMs;

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
        private bool _gcDisabledForMonitoring;
    }
}

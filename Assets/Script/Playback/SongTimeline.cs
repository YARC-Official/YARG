using System;

namespace YARG.Playback
{
    /// <summary>
    /// Immutable view of song timeline at one point in time.
    /// Use <see cref="InputTime"/> for gameplay: input handling, note judgments, engines, and replays.
    /// Use <see cref="SongTime"/> for audio-relative work, such as audio playback and audio-synced events.
    /// Use <see cref="VisualTime"/> for rendering, animations, and other video-synced work.
    /// </summary>
    internal readonly struct SongTimelineSnapshot
    {
        /// <summary>
        /// Gameplay time in seconds. Use for input handling, note judgments, engines, and replays.
        /// </summary>
        public readonly double InputTime;

        /// <summary>
        /// Audio-relative time in seconds. Use for audio playback and audio-synced events. Includes audio calibration.
        /// </summary>
        public readonly double SongTime;

        /// <summary>
        /// Render-relative time in seconds. Use for rendering, animations, and video-synced events. Includes video calibration.
        /// </summary>
        public readonly double VisualTime;

        /// <summary>
        /// Input system timestamp that maps to zero gameplay input time.
        /// </summary>
        public readonly double InputTimeOffset;

        /// <summary>
        /// Current playback speed, where 1.0 is normal speed.
        /// </summary>
        public readonly float SongSpeed;

        /// <summary>
        /// Audio calibration in seconds before speed scaling.
        /// </summary>
        public readonly double AudioCalibration;

        /// <summary>
        /// Video calibration in seconds before speed scaling.
        /// </summary>
        public readonly double VideoCalibration;

        public SongTimelineSnapshot(
            double inputTime,
            double songTime,
            double visualTime,
            double inputTimeOffset,
            float songSpeed,
            double audioCalibration,
            double videoCalibration)
        {
            InputTime = inputTime;
            SongTime = songTime;
            VisualTime = visualTime;
            InputTimeOffset = inputTimeOffset;
            SongSpeed = songSpeed;
            AudioCalibration = audioCalibration;
            VideoCalibration = videoCalibration;
        }
    }

    internal sealed class SongTimeline
    {
        private readonly object _lock = new();
        private readonly IInputClock _inputClock;

        private SongTimelineSnapshot _current;
        private double _audioCalibration;
        private double _videoCalibration;
        private double _lastInputSystemTime;

        public SongTimeline(
            IInputClock inputClock,
            float songSpeed,
            double audioCalibration,
            double videoCalibration)
        {
            _inputClock = inputClock;
            _audioCalibration = audioCalibration;
            _videoCalibration = videoCalibration;
            _lastInputSystemTime = inputClock.FrameTime;
            _current = BuildSnapshot(0.0, 0.0, songSpeed);
        }

        /// <summary>
        /// Latest timeline snapshot.
        /// </summary>
        public SongTimelineSnapshot Current
        {
            get
            {
                lock (_lock)
                {
                    return _current;
                }
            }
        }

        /// <summary>
        /// Updates the snapshot using the current frame input time without changing the timeline mapping.
        /// </summary>
        public void TickFrame()
        {
            TickAt(_inputClock.FrameTime);
        }

        /// <summary>
        /// Updates the snapshot at the given input system time without changing the timeline mapping.
        /// </summary>
        public void TickAt(double inputSystemTime)
        {
            lock (_lock)
            {
                UpdateSnapshot(inputSystemTime, _current.InputTimeOffset, _current.SongSpeed);
            }
        }

        /// <summary>
        /// Rebases the timeline so the current frame input time maps to the target input time.
        /// </summary>
        public void AnchorAtFrame(double targetInputTime)
        {
            lock (_lock)
            {
                SetAnchor(targetInputTime, _inputClock.FrameTime);
            }
        }

        /// <summary>
        /// Rebases the timeline so the current instant input time maps to the target input time.
        /// </summary>
        public void AnchorAtInstant(double targetInputTime)
        {
            double inputSystemTime = _inputClock.ClampFrameTimeToInstant();
            lock (_lock)
            {
                SetAnchor(targetInputTime, inputSystemTime);
            }
        }

        private void SetAnchor(double targetInputTime, double inputSystemTime)
        {
            double inputTimeOffset = CalculateInputTimeOffset(inputSystemTime, targetInputTime, _current.SongSpeed);
            UpdateSnapshot(inputSystemTime, inputTimeOffset, _current.SongSpeed);
        }

        /// <summary>
        /// Converts an input system timestamp to timeline input time using the current mapping.
        /// </summary>
        public double ConvertInputSystemTime(double inputSystemTime)
        {
            lock (_lock)
            {
                return CalculateInputTime(inputSystemTime, _current.InputTimeOffset, _current.SongSpeed);
            }
        }

        /// <summary>
        /// Reads a timeline snapshot at the given input system time without mutating current timeline state.
        /// </summary>
        public SongTimelineSnapshot GetSnapshotAt(double inputSystemTime)
        {
            lock (_lock)
            {
                return BuildSnapshot(inputSystemTime, _current.InputTimeOffset, _current.SongSpeed);
            }
        }

        /// <summary>
        /// Changes song speed while preserving the input time at the given input system time.
        /// </summary>
        public void ApplySpeedChange(float newSpeed, double inputSystemTime)
        {
            lock (_lock)
            {
                double currentInputAtChange = CalculateInputTime(
                    inputSystemTime,
                    _current.InputTimeOffset,
                    _current.SongSpeed);
                double inputTimeOffset = CalculateInputTimeOffset(inputSystemTime, currentInputAtChange, newSpeed);
                double snapshotInputSystemTime = Math.Max(_lastInputSystemTime, inputSystemTime);
                UpdateSnapshot(snapshotInputSystemTime, inputTimeOffset, newSpeed);
            }
        }

        /// <summary>
        /// Updates calibration values and rebases the timeline at the current frame input time.
        /// </summary>
        public void UpdateCalibrationAndAnchor(double audioCalibration,
            double videoCalibration,
            double targetInputTime)
        {
            lock (_lock)
            {
                double inputSystemTime = Math.Max(_lastInputSystemTime, _inputClock.FrameTime);
                _audioCalibration = audioCalibration;
                _videoCalibration = videoCalibration;
                SetAnchor(targetInputTime, inputSystemTime);
            }
        }

        private void UpdateSnapshot(double inputSystemTime, double inputTimeOffset, float songSpeed)
        {
            _lastInputSystemTime = inputSystemTime;
            _current = BuildSnapshot(inputSystemTime, inputTimeOffset, songSpeed);
        }

        private static double CalculateInputTime(double inputSystemTime, double inputTimeOffset, float songSpeed)
        {
            return (inputSystemTime - inputTimeOffset) * songSpeed;
        }

        private static double CalculateInputTimeOffset(double inputSystemTime, double targetInputTime, float songSpeed)
        {
            return inputSystemTime - (targetInputTime / songSpeed);
        }

        private SongTimelineSnapshot BuildSnapshot(double inputSystemTime, double inputTimeOffset, float songSpeed)
        {
            double inputTime = CalculateInputTime(inputSystemTime, inputTimeOffset, songSpeed);
            double songTime = inputTime + (_audioCalibration * songSpeed);
            double visualTime = inputTime + (_videoCalibration * songSpeed);
            return new SongTimelineSnapshot(
                inputTime,
                songTime,
                visualTime,
                inputTimeOffset,
                songSpeed,
                _audioCalibration,
                _videoCalibration
            );
        }
    }
}

using System;

namespace YARG.Playback
{
    internal readonly struct SongTimelineSnapshot
    {
        public readonly double InputTime;
        public readonly double SongTime;
        public readonly double VisualTime;
        public readonly double InputTimeOffset;
        public readonly float SongSpeed;

        public SongTimelineSnapshot(
            double inputTime,
            double songTime,
            double visualTime,
            double inputTimeOffset,
            float songSpeed)
        {
            InputTime = inputTime;
            SongTime = songTime;
            VisualTime = visualTime;
            InputTimeOffset = inputTimeOffset;
            SongSpeed = songSpeed;
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
        /// Audio calibration applied to input time when calculating song time.
        /// </summary>
        public double AudioCalibration
        {
            get
            {
                lock (_lock)
                {
                    return _audioCalibration;
                }
            }
        }

        /// <summary>
        /// Video calibration applied to input time when calculating visual time.
        /// </summary>
        public double VideoCalibration
        {
            get
            {
                lock (_lock)
                {
                    return _videoCalibration;
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
                _lastInputSystemTime = inputSystemTime;
                _current = BuildSnapshot(inputSystemTime, _current.InputTimeOffset, _current.SongSpeed);
            }
        }

        /// <summary>
        /// Rebases the timeline so the current frame input time maps to the target input time.
        /// </summary>
        public void AnchorAtFrame(double targetInputTime)
        {
            AnchorAt(targetInputTime, _inputClock.FrameTime);
        }

        /// <summary>
        /// Rebases the timeline so the current instant input time maps to the target input time.
        /// </summary>
        public void AnchorAtInstant(double targetInputTime)
        {
            double inputSystemTime = _inputClock.ClampFrameTimeToInstant();
            AnchorAt(targetInputTime, inputSystemTime);
        }

        private void AnchorAt(double targetInputTime, double inputSystemTime)
        {
            lock (_lock)
            {
                double inputTimeOffset = inputSystemTime - (targetInputTime / _current.SongSpeed);
                _lastInputSystemTime = inputSystemTime;
                _current = BuildSnapshot(inputSystemTime, inputTimeOffset, _current.SongSpeed);
            }
        }

        /// <summary>
        /// Converts an input system timestamp to timeline input time using the current mapping.
        /// </summary>
        public double ConvertInputSystemTime(double inputSystemTime)
        {
            lock (_lock)
            {
                return CalculateInputTime(inputSystemTime);
            }
        }

        /// <summary>
        /// Changes song speed while preserving the input time at the given input system time.
        /// </summary>
        public void ApplySpeedChange(float newSpeed, double inputSystemTime)
        {
            lock (_lock)
            {
                double currentInputAtChange = CalculateInputTime(inputSystemTime);
                double inputTimeOffset = inputSystemTime - (currentInputAtChange / newSpeed);
                double snapshotInputSystemTime = Math.Max(_lastInputSystemTime, inputSystemTime);
                _lastInputSystemTime = snapshotInputSystemTime;
                _current = BuildSnapshot(snapshotInputSystemTime, inputTimeOffset, newSpeed);
            }
        }

        /// <summary>
        /// Updates calibration values and rebases the timeline at the current frame input time.
        /// </summary>
        public void SetCalibrationAndAnchorAtFrame(double audioCalibration,
            double videoCalibration,
            double targetInputTime)
        {
            lock (_lock)
            {
                double inputSystemTime = _inputClock.FrameTime;
                _audioCalibration = audioCalibration;
                _videoCalibration = videoCalibration;
                double inputTimeOffset = inputSystemTime - (targetInputTime / _current.SongSpeed);
                _lastInputSystemTime = inputSystemTime;
                _current = BuildSnapshot(inputSystemTime, inputTimeOffset, _current.SongSpeed);
            }
        }


        private double CalculateInputTime(double systemTime)
        {
            return (systemTime - _current.InputTimeOffset) * _current.SongSpeed;
        }

        private SongTimelineSnapshot BuildSnapshot(double inputSystemTime, double inputTimeOffset, float songSpeed)
        {
            double inputTime = (inputSystemTime - inputTimeOffset) * songSpeed;
            double songTime = inputTime + (_audioCalibration * songSpeed);
            double visualTime = inputTime + (_videoCalibration * songSpeed);
            return new SongTimelineSnapshot(inputTime, songTime, visualTime, inputTimeOffset, songSpeed);
        }
    }
}

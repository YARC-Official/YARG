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

        public SongTimelineSnapshot TickFrame()
        {
            return TickAt(_inputClock.FrameTime);
        }

        public SongTimelineSnapshot TickAt(double inputSystemTime)
        {
            lock (_lock)
            {
                _lastInputSystemTime = inputSystemTime;
                _current = BuildSnapshot(inputSystemTime, _current.InputTimeOffset, _current.SongSpeed);
                return _current;
            }
        }

        public SongTimelineSnapshot AnchorAtFrame(double targetInputTime)
        {
            return AnchorAt(targetInputTime, _inputClock.FrameTime);
        }

        public SongTimelineSnapshot AnchorAtInstant(double targetInputTime)
        {
            double inputSystemTime = _inputClock.ClampFrameTimeToInstant();
            return AnchorAt(targetInputTime, inputSystemTime);
        }

        public SongTimelineSnapshot AnchorAt(double targetInputTime, double inputSystemTime)
        {
            lock (_lock)
            {
                double inputTimeOffset = inputSystemTime - (targetInputTime / _current.SongSpeed);
                _lastInputSystemTime = inputSystemTime;
                _current = BuildSnapshot(inputSystemTime, inputTimeOffset, _current.SongSpeed);
                return _current;
            }
        }

        public double ConvertInputSystemTime(double inputSystemTime)
        {
            lock (_lock)
            {
                return ConvertInputSystemTimeLocked(inputSystemTime);
            }
        }

        public SongTimelineSnapshot ApplySpeedChangeInstant(float newSpeed)
        {
            return ApplySpeedChange(newSpeed, _inputClock.InstantTime);
        }

        public SongTimelineSnapshot ApplySpeedChange(float newSpeed, double inputSystemTime)
        {
            lock (_lock)
            {
                double currentInputAtChange = ConvertInputSystemTimeLocked(inputSystemTime);
                double inputTimeOffset = inputSystemTime - (currentInputAtChange / newSpeed);
                double snapshotInputSystemTime = Math.Max(_lastInputSystemTime, inputSystemTime);
                _lastInputSystemTime = snapshotInputSystemTime;
                _current = BuildSnapshot(snapshotInputSystemTime, inputTimeOffset, newSpeed);
                return _current;
            }
        }

        public SongTimelineSnapshot SetCalibration(double audioCalibration, double videoCalibration)
        {
            lock (_lock)
            {
                _audioCalibration = audioCalibration;
                _videoCalibration = videoCalibration;
                _current = BuildSnapshot(_lastInputSystemTime, _current.InputTimeOffset, _current.SongSpeed);
                return _current;
            }
        }

        public SongTimelineSnapshot SetCalibrationAndAnchorAtFrame(
            double audioCalibration,
            double videoCalibration,
            double targetInputTime)
        {
            return SetCalibrationAndAnchorAt(audioCalibration, videoCalibration, targetInputTime, _inputClock.FrameTime);
        }

        public SongTimelineSnapshot SetCalibrationAndAnchorAt(
            double audioCalibration,
            double videoCalibration,
            double targetInputTime,
            double inputSystemTime)
        {
            lock (_lock)
            {
                _audioCalibration = audioCalibration;
                _videoCalibration = videoCalibration;
                double inputTimeOffset = inputSystemTime - (targetInputTime / _current.SongSpeed);
                _lastInputSystemTime = inputSystemTime;
                _current = BuildSnapshot(inputSystemTime, inputTimeOffset, _current.SongSpeed);
                return _current;
            }
        }

        private double ConvertInputSystemTimeLocked(double inputSystemTime)
        {
            return (inputSystemTime - _current.InputTimeOffset) * _current.SongSpeed;
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

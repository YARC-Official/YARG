using System;
using System.Collections.Generic;
using System.Diagnostics;
using YARG.Core.Audio;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Models mixer playback without exposing tempo and output-buffer delay to callers.
    /// </summary>
    internal sealed class BufferedPlaybackTimeline
    {
        private readonly Func<double> _getTime;
        private readonly PositionModel _immediate = new();
        private readonly PositionModel _tempoOutput = new();

        private double _retainedHistorySeconds;
        private double _outputLatency;
        private float _songSpeed;
        private float _syncAdjustment;
        private bool _isPlaying;

        public double OutputLatency => _outputLatency;

        public BufferedPlaybackTimeline(float speed)
            : this(speed, GetCurrentTime)
        {
        }

        internal BufferedPlaybackTimeline(float speed, Func<double> getTime)
        {
            _songSpeed = speed;
            _getTime = getTime;
            Reset(0);
        }

        public PlaybackPosition GetPosition(double rawPosition, double tempoLatency)
        {
            double now = _getTime();
            tempoLatency = Math.Max(0, tempoLatency);
            _retainedHistorySeconds = Math.Max(
                _retainedHistorySeconds,
                tempoLatency + Math.Abs(_outputLatency));

            double modeledTempoPosition = _tempoOutput.GetPosition(now);
            double modeledHeardPosition = _tempoOutput.GetPosition(now - _outputLatency);

            // Correct modeled output with observed BASS position. Same correction is applied to
            // delay-free model, which is Smith-predictor feedback for model/clock drift.
            double modelError = rawPosition - modeledTempoPosition;
            double heardPosition = modeledHeardPosition + modelError;
            double controlPosition = _immediate.GetPosition(now) + modelError;

            double cutoff = now - _retainedHistorySeconds - 1.0;
            _immediate.PruneBefore(cutoff);
            _tempoOutput.PruneBefore(cutoff);
            return new PlaybackPosition(heardPosition, controlPosition);
        }

        public void SetOutputLatency(double latency)
        {
            double latencyChange = latency - _outputLatency;
            if (latencyChange != 0)
            {
                _immediate.Shift(-latencyChange * CurrentRate);
            }

            _outputLatency = latency;
            _retainedHistorySeconds = Math.Max(_retainedHistorySeconds, Math.Abs(_outputLatency));
        }

        public void SetSpeed(float songSpeed, float syncAdjustment, double tempoLatency)
        {
            if (_songSpeed == songSpeed && _syncAdjustment == syncAdjustment)
            {
                return;
            }

            _songSpeed = songSpeed;
            _syncAdjustment = syncAdjustment;
            if (!_isPlaying)
            {
                return;
            }

            double now = _getTime();
            float rate = CurrentRate;
            _immediate.SetRate(now, rate);
            _tempoOutput.SetRate(now + Math.Max(0, tempoLatency), rate);
        }

        public void Play()
        {
            if (_isPlaying)
            {
                return;
            }

            _isPlaying = true;
            double now = _getTime();
            _immediate.SetRate(now, CurrentRate);
            _tempoOutput.SetRate(now, CurrentRate);
        }

        public void Pause()
        {
            if (!_isPlaying)
            {
                return;
            }

            _isPlaying = false;
            double now = _getTime();
            _immediate.Stop(now);
            _tempoOutput.Stop(now);
        }

        public void Reset(double songPosition)
        {
            double now = _getTime();
            float rate = _isPlaying ? CurrentRate : 0f;
            double controlPosition = songPosition - _outputLatency * CurrentRate;
            _immediate.Reset(now, controlPosition, rate);
            _tempoOutput.Reset(now, songPosition, rate);
        }

        private float CurrentRate => _songSpeed + _syncAdjustment;

        private static double GetCurrentTime()
        {
            return (double) Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        }

        private sealed class PositionModel
        {
            private readonly List<RateChange> _changes = new();
            private double _startPosition;

            public void Reset(double timestamp, double position, float rate)
            {
                _startPosition = position;
                _changes.Clear();
                _changes.Add(new RateChange(timestamp, rate));
            }

            public void Shift(double positionDelta)
            {
                _startPosition += positionDelta;
            }

            public void Stop(double timestamp)
            {
                RemoveChangesAfter(timestamp);
                SetRate(timestamp, 0f);
            }

            public void SetRate(double timestamp, float rate)
            {
                for (int i = 0; i < _changes.Count; i++)
                {
                    if (_changes[i].Timestamp == timestamp)
                    {
                        _changes[i] = new RateChange(timestamp, rate);
                        return;
                    }

                    if (_changes[i].Timestamp > timestamp)
                    {
                        _changes.Insert(i, new RateChange(timestamp, rate));
                        return;
                    }
                }

                _changes.Add(new RateChange(timestamp, rate));
            }

            public double GetPosition(double timestamp)
            {
                RateChange first = _changes[0];
                if (timestamp <= first.Timestamp)
                {
                    return _startPosition;
                }

                double position = _startPosition;
                double intervalStart = first.Timestamp;
                float rate = first.Rate;
                for (int i = 1; i < _changes.Count; i++)
                {
                    RateChange change = _changes[i];
                    if (change.Timestamp > timestamp)
                    {
                        break;
                    }

                    position += rate * (change.Timestamp - intervalStart);
                    intervalStart = change.Timestamp;
                    rate = change.Rate;
                }

                return position + rate * (timestamp - intervalStart);
            }

            public void PruneBefore(double timestamp)
            {
                if (_changes.Count < 2 || _changes[1].Timestamp > timestamp)
                {
                    return;
                }

                double position = GetPosition(timestamp);
                float rate = GetRate(timestamp);
                int removeCount = 0;
                while (removeCount < _changes.Count && _changes[removeCount].Timestamp <= timestamp)
                {
                    removeCount++;
                }

                _changes.RemoveRange(0, removeCount);
                _changes.Insert(0, new RateChange(timestamp, rate));
                _startPosition = position;
            }

            private float GetRate(double timestamp)
            {
                float rate = _changes[0].Rate;
                for (int i = 1; i < _changes.Count && _changes[i].Timestamp <= timestamp; i++)
                {
                    rate = _changes[i].Rate;
                }
                return rate;
            }

            private void RemoveChangesAfter(double timestamp)
            {
                int index = _changes.FindIndex(change => change.Timestamp > timestamp);
                if (index >= 0)
                {
                    _changes.RemoveRange(index, _changes.Count - index);
                }
            }

            private readonly struct RateChange
            {
                public readonly double Timestamp;
                public readonly float Rate;

                public RateChange(double timestamp, float rate)
                {
                    Timestamp = timestamp;
                    Rate = rate;
                }
            }
        }
    }
}

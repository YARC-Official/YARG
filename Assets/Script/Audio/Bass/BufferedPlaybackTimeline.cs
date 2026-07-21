using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Converts current BASS position into delay-free control position used for gameplay synchronization.
    /// </summary>
    /// <remarks>
    /// BASS position is the source of truth. Rate histories track remaining BASS buffer time so
    /// synchronization can account for commands that have not taken effect yet.
    ///
    /// Two histories represent BASS command buffering:
    /// <list type="bullet">
    /// <item><see cref="_commandedRateHistory"/> applies commands immediately.</item>
    /// <item><see cref="_bufferedRateHistory"/> applies commands after their measured BASS latency.</item>
    /// </list>
    /// Differences between these histories reveal song progress hidden by the remaining BASS buffer.
    /// </remarks>
    internal sealed class BufferedPlaybackTimeline
    {
        private const double HISTORY_MARGIN_SECONDS = 1.0;
        private readonly PlaybackRateHistory _commandedRateHistory = new();
        private readonly PlaybackRateHistory _bufferedRateHistory  = new();

        private float   _songSpeed;
        private float   _syncAdjustment;
        private bool    _isPlaying;
        private double? _preparedControlPosition;

        public double OutputLatency { get; private set; }
        public BufferedPlaybackTimeline(float speed)
        {
            _songSpeed = speed;
            double now = GetCurrentTime();
            _commandedRateHistory.Reset(now, 0, 0f);
            _bufferedRateHistory.Reset(now, 0, 0f);
        }

        /// <summary>
        /// Converts current BASS position into delay-free control position used to synchronize playback.
        /// </summary>
        /// <param name="bassPosition">Current song position read from BASS.</param>
        /// <returns>Position with pending BASS command-buffer progress applied.</returns>
        /// <remarks>
        /// <para>
        /// Mathematical relationships:
        /// <c>Control = Raw BASS Position + (Commanded Integral - Buffered Integral)</c>
        /// </para>
        /// <para>
        /// Playback speed changes do not take effect immediately. Control position accounts for that delay,
        /// allowing synchronization to calculate error as:
        /// <c>error = target time - Control</c>
        /// </para>
        /// <para>
        /// We can then use that error to predict a speed adjustment in <c>AudioSynchronizer.Synchronize</c>.
        /// </para>
        /// <para>
        /// BASS position remains the source of truth. Rate histories only account for commands still
        /// pending in the BASS buffer.
        /// </para>
        /// </remarks>
        public double GetControlPosition(double bassPosition)
        {
            double now = GetCurrentTime();

            // Command history changes rate immediately; buffered history changes rate when the remaining
            // BASS buffer reaches command. Their difference removes that buffer delay from the
            // synchronization position.
            double commandBufferingOffset =
                _commandedRateHistory.GetPositionAt(now) - _bufferedRateHistory.GetPositionAt(now);
            double controlPosition = bassPosition + commandBufferingOffset;

            // Re-anchor old history periodically. Pruning preserves all positions from the cutoff onward.
            double cutoff = now - HISTORY_MARGIN_SECONDS;
            _commandedRateHistory.PruneBefore(cutoff);
            _bufferedRateHistory.PruneBefore(cutoff);
            return controlPosition;
        }

        /// <summary>
        /// Sets calibrated delay between BASS tempo output and audio heard by player.
        /// </summary>
        /// <remarks>
        /// Moving the command history by the same amount makes synchronization move playback toward the
        /// newly calibrated target instead of treating calibration as a reporting-only change.
        /// </remarks>
        public void SetOutputLatency(double latency)
        {
            double latencyChange = latency - OutputLatency;
            if (latencyChange != 0)
            {
                _commandedRateHistory.Shift(-latencyChange * CurrentRate);
            }

            OutputLatency = latency;
        }

        /// <summary>
        /// Records a speed command. Synchronization uses the new speed immediately, while BASS tempo
        /// output does not reflect it until audio already buffered by the tempo stream has played.
        /// </summary>
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

            double now = GetCurrentTime();
            float rate = CurrentRate;
            _commandedRateHistory.SetRate(now, rate);
            _bufferedRateHistory.SetRate(now + Math.Max(0, tempoLatency), rate);
        }

        /// <summary>
        /// Starts commanded and buffered position advancement.
        /// </summary>
        public void Play(double bassPosition)
        {
            if (_isPlaying)
            {
                return;
            }

            _isPlaying = true;
            double now = GetCurrentTime();
            float rate = CurrentRate;
            if (_preparedControlPosition.HasValue)
            {
                // ChannelPlay can let one device/update quantum advance before it returns. Gameplay is
                // anchored after that call, so make the position observed here represent the prepared
                // control position instead of exposing that backend startup advancement as sync error.
                // The compensated BASS position then remains still until the device buffer begins
                // playing, so advance only the commanded history during that startup interval.
                double controlPosition = _preparedControlPosition.Value;
                _commandedRateHistory.Reset(now, controlPosition, rate);
                _bufferedRateHistory.Reset(now, bassPosition, 0f);
                _bufferedRateHistory.SetRate(now + BassLatencyProvider.StartupLatency, rate);
                _preparedControlPosition = null;
                return;
            }

            _commandedRateHistory.SetRate(now, rate);
            _bufferedRateHistory.SetRate(now + BassLatencyProvider.StartupLatency, rate);
        }

        /// <summary>
        /// Stops both position histories at the current time and removes commands that had not yet
        /// reached buffered output.
        /// </summary>
        public void Pause()
        {
            if (!_isPlaying)
            {
                return;
            }

            _isPlaying = false;
            double now = GetCurrentTime();
            _commandedRateHistory.Stop(now);
            _bufferedRateHistory.Stop(now);
        }

        /// <summary>
        /// Re-anchors both histories after the mixer has prepared a seek.
        /// </summary>
        /// <param name="observedPosition">Position now reported by the prepared BASS streams.</param>
        /// <param name="requestedPosition">Position requested by the playback caller.</param>
        public void ResetAfterSeek(double observedPosition, double requestedPosition)
        {
            double now = GetCurrentTime();
            float rate = _isPlaying ? CurrentRate : 0f;
            _commandedRateHistory.Reset(now, requestedPosition, rate);
            _bufferedRateHistory.Reset(now, observedPosition, rate);
            _preparedControlPosition = requestedPosition;
        }

        private float CurrentRate => _songSpeed + _syncAdjustment;

        private static double GetCurrentTime()
        {
            return (double) Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        }

        /// <summary>
        /// Playback-rate history used to calculate song progress between timestamps.
        /// </summary>
        private sealed class PlaybackRateHistory
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

            public double GetPositionAt(double timestamp)
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

                double position = GetPositionAt(timestamp);
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

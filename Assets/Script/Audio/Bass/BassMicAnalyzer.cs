#nullable enable
using System;
using System.Collections.Concurrent;
using System.Threading;
using ManagedBass;
using UnityEngine;
using YARG.Audio.PitchDetection;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Supplies microphone samples to the background analyzer.
    /// </summary>
    internal interface IBassMicSampleSource
    {
        int  SampleRate { get; }
        bool IsValid    { get; }
        int Read(Span<float> destination);
        int GetBacklogBytes();
        bool ResetToLive();
    }

    /// <summary>
    ///     Reads microphone samples on a dedicated background thread, running pitch detection and volume analysis
    ///     to generate timed input frames for vocal gameplay without blocking the main game loop.
    /// </summary>
    internal sealed class BassMicAnalyzer : IDisposable
    {
        private const float HIT_THRESHOLD_DB  = 25f;
        private const float SILENCE_FLOOR_DB  = -160f;
        private const float CALIBRATION_GAIN  = 180f;
        private const float UNAVAILABLE_VALUE = -1f;

        private const int AMPLITUDE_STRIDE   = 4;
        private const int IDLE_SLEEP_MS      = 1;

        private readonly object                          _analysisLock = new();
        private readonly float[]                         _frameBuffer;
        private readonly Func<double>                    _getInputTime;
        private readonly Func<bool>                      _isOutputRecording;
        private readonly ConcurrentQueue<MicOutputFrame> _outputFrames = new();
        private readonly PitchTracker                    _pitchTracker;

        private readonly float[] _readBuffer;

        private readonly IBassMicSampleSource _source;
        private readonly Thread               _worker;
        private          bool                 _failureLogged;
        private          int                  _frameSamples;
        private          float?               _lastAmplitude;

        private float? _lastPitch;
        private bool   _paused;

        private volatile bool _stopRequested;

        public BassMicAnalyzer(IBassMicSampleSource source, Func<bool> isOutputRecording, Func<double> getInputTime)
        {
            _source = source;
            _isOutputRecording = isOutputRecording;
            _getInputTime = getInputTime;

            int samplesPerFrame = checked(source.SampleRate * MicDevice.RECORD_PERIOD_MS / 1000);
            _readBuffer = new float[samplesPerFrame];
            _frameBuffer = new float[samplesPerFrame];
            _pitchTracker = new PitchTracker(source.SampleRate);

            _worker = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = $"Mic analysis {source.SampleRate} Hz",
            };
            _worker.Start();
        }

        public void Dispose() => StopAndJoin();

        public bool Reset()
        {
            lock (_analysisLock)
            {
                bool reset = _source.ResetToLive();
                ClearState();
                _paused = false;
                return reset;
            }
        }

        public bool StopAndJoin()
        {
            _stopRequested = true;

            if (Thread.CurrentThread == _worker)
            {
                return true;
            }

            if (!_worker.Join(1000))
            {
                YargLogger.LogError("Timed out waiting for microphone analysis worker to stop");
                _worker.Join();
            }

            return true;
        }

        public bool DequeueOutputFrame(out MicOutputFrame frame) => _outputFrames.TryDequeue(out frame);

        public void ClearOutputQueue() => _outputFrames.Clear();

        private void ReadLoop()
        {
            try
            {
                while (!_stopRequested && _source.IsValid)
                {
                    if (!_isOutputRecording())
                    {
                        PauseAnalysis();
                        Thread.Sleep(IDLE_SLEEP_MS);
                        continue;
                    }

                    if (!TryReadSamples())
                    {
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Microphone analysis worker failed");
            }
        }

        private void PauseAnalysis()
        {
            lock (_analysisLock)
            {
                if (_paused)
                {
                    return;
                }

                if (_source.IsValid && !_source.ResetToLive())
                {
                    LogFailure("Failed to reset disabled microphone analysis source");
                    return;
                }

                ClearState();
                _paused = true;
            }
        }

        private bool TryReadSamples()
        {
            lock (_analysisLock)
            {
                if (_stopRequested || !_source.IsValid)
                {
                    return false;
                }

                _paused = false;

                int backlogBytes = _source.GetBacklogBytes();
                if (backlogBytes < 0)
                {
                    LogFailure("Failed to query microphone analysis backlog");
                    return false;
                }

                double readTime = _getInputTime();
                int samplesRead = _source.Read(_readBuffer.AsSpan());

                if (samplesRead < 0)
                {
                    LogFailure("Failed to read microphone analysis samples");
                    return false;
                }

                if (samplesRead == 0)
                {
                    Thread.Sleep(IDLE_SLEEP_MS);
                }
                else
                {
                    AssembleFrames(_readBuffer, samplesRead, readTime, backlogBytes);
                }

                return true;
            }
        }

        private void AssembleFrames(float[] samples, int sampleCount, double readTime, int backlogBytesBeforeRead)
        {
            int backlogSamples = backlogBytesBeforeRead / sizeof(float);
            int offset = 0;

            while (offset < sampleCount)
            {
                int frameSpace = _frameBuffer.Length - _frameSamples;
                int samplesToCopy = Math.Min(sampleCount - offset, frameSpace);
                Array.Copy(samples, offset, _frameBuffer, _frameSamples, samplesToCopy);

                _frameSamples += samplesToCopy;
                offset += samplesToCopy;

                int samplesStillAhead = Math.Max(0, backlogSamples - offset);
                double frameEndTime = readTime - samplesStillAhead / (double) _source.SampleRate;

                if (_frameSamples == _frameBuffer.Length)
                {
                    AnalyzeFrame(_frameBuffer, frameEndTime);
                    _frameSamples = 0;
                }
            }
        }

        private void AnalyzeFrame(float[] samples, double frameEndTime)
        {
            float amplitude = MeasureAmplitude(samples);

            if (_lastAmplitude is { } previousAmplitude && amplitude - previousAmplitude >= HIT_THRESHOLD_DB)
            {
                double midpoint = frameEndTime - MicDevice.RECORD_PERIOD_MS / 2000.0;
                _outputFrames.Enqueue(new MicOutputFrame(midpoint, true, UNAVAILABLE_VALUE, UNAVAILABLE_VALUE));
            }

            _lastAmplitude = amplitude;

            if (amplitude < SettingsManager.Settings.MicrophoneSensitivity.Value)
            {
                _lastPitch = null;
                return;
            }

            _lastPitch = _pitchTracker.ProcessBuffer(samples) ?? _lastPitch;

            if (_lastPitch is { } pitch)
            {
                _outputFrames.Enqueue(new MicOutputFrame(frameEndTime, false, pitch, amplitude));
            }
        }

        private static float MeasureAmplitude(ReadOnlySpan<float> samples)
        {
            float sumOfSquares = 0f;
            int sampleCount = 0;

            for (int i = 0; i < samples.Length; i += AMPLITUDE_STRIDE)
            {
                sumOfSquares += samples[i] * samples[i];
                sampleCount++;
            }

            float rootMeanSquare = Mathf.Sqrt(sumOfSquares / sampleCount);
            float decibels = 20f * Mathf.Log10(rootMeanSquare * CALIBRATION_GAIN);

            if (decibels < SILENCE_FLOOR_DB || float.IsNaN(decibels))
            {
                return SILENCE_FLOOR_DB;
            }

            return decibels;
        }

        private void ClearState()
        {
            _lastPitch = null;
            _lastAmplitude = null;
            _frameSamples = 0;
            Array.Clear(_frameBuffer, 0, _frameBuffer.Length);
            _pitchTracker.Reset();
            _outputFrames.Clear();
        }

        private void LogFailure(string message)
        {
            if (_failureLogged)
            {
                return;
            }

            _failureLogged = true;
            YargLogger.LogError($"{message}: {Bass.LastError}");
        }
    }
}

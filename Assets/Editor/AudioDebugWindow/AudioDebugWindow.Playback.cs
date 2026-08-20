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

    }
}

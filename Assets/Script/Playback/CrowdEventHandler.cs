using System;
using System.Collections.Generic;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Parsing;
using YARG.Gameplay;
using YARG.Settings;

namespace YARG.Playback
{
    public enum CrowdFxMode
    {
        Disabled,
        StarpowerClapsOnly,
        Enabled
    }

    public class CrowdEventHandler : IDisposable
    {
        public CrowdState CrowdState     { get; private set; } = CrowdState.Realtime;
        public ClapState  ClapState { get; private set; } = ClapState.Clap;

        private bool IsCrowdMuted = false;

        private readonly List<CrowdEvent> _events;
        private readonly GameManager      _gameManager;
        private readonly SyncTrack        _syncTrack;
        private readonly EngineManager    _engineManager;

        private SfxSample[] _startSamples = { SfxSample.CrowdStart, SfxSample.CrowdStart2, SfxSample.CrowdStart3 };

        private SfxSample _selectedStartSample;
        // Only one for now, but more will come
        private SfxSample _selectedEndSample = SfxSample.CrowdEnd;

        private int _eventIndex;

        private bool _endSamplePlayed;
        private bool _disposed;

        public CrowdEventHandler(SongChart chart, GameManager gameManager)
        {
            // Clone the event list so we can modify it if necessary
            _events = new List<CrowdEvent>(chart.CrowdEvents);
            _syncTrack = chart.SyncTrack;
            _gameManager = gameManager;
            _engineManager = gameManager.EngineManager;

            // If crowd fx is disabled, don't bother subscribing to beat events
            if (SettingsManager.Settings.UseCrowdFx.Value != CrowdFxMode.Disabled)
            {
                // Clap sample takes 20ms to actually hit (not sure if this should actually be measure or strongbeat)
                _gameManager.BeatEventHandler?.Audio.Subscribe(Clap, BeatEventType.StrongBeat, offset: -0.02);
            }
            else if (SettingsManager.Settings.UseCrowdFx.Value == CrowdFxMode.StarpowerClapsOnly)
            {
                ChangeCrowdMuteState(true);
            }

            if (SettingsManager.Settings.UseCrowdFx.Value == CrowdFxMode.Enabled)
            {
                _selectedStartSample = _startSamples[UnityEngine.Random.Range(0, _startSamples.Length)];
                GlobalAudioHandler.PlaySoundEffect(_selectedStartSample);
            }

            if (SettingsManager.Settings.NoFailMode.Value || GlobalVariables.State.IsPractice)
            {
                return;
            }

            if (_gameManager.ReplayInfo == null || GlobalVariables.State.PlayingWithReplay)
            {
                _engineManager.OnSongFailed += OnSongFailed;

                if (SettingsManager.Settings.UseCrowdFx.Value == CrowdFxMode.Enabled)
                {
                    _engineManager.OnHappinessUnderThreshold += OnHappinessUnderThreshold;
                    _engineManager.OnHappinessOverThreshold += OnHappinessOverThreshold;
                }
            }
        }

        public void Update(double time)
        {
            while (_eventIndex < _events.Count && _events[_eventIndex].Time <= time)
            {
                var ev = _events[_eventIndex];

                switch (ev.Type)
                {
                    case CrowdEvent.CrowdEventType.Clap:
                        ClapState = ev.ClapState;
                        break;
                    case CrowdEvent.CrowdEventType.State:
                        CrowdState = ev.CrowdState;
                        break;
                }
                _eventIndex++;
            }

            if (time >= _gameManager.LastNoteTime)
            {
                // Play the end sample if it hasn't been played yet
                if (!_endSamplePlayed)
                {
                    _endSamplePlayed = true;
                    GlobalAudioHandler.PlaySoundEffect(_selectedEndSample);
                }
            }
        }

        private void Clap()
        {
            // No clapping when charter has inhibited clapping, even if SP is active
            if (ClapState == ClapState.NoClap)
            {
                return;
            }

            // No clapping before first note or after last note (in case charter forgot to put crowd back in realtime)
            if (_gameManager.SongTime < _gameManager.FirstNoteTime || _gameManager.SongTime > _gameManager.LastNoteTime)
            {
                return;
            }

            // Only clap when happiness meter is full or SP is active
            if (_gameManager.EngineManager.Happiness < 1.0f && _gameManager.StarPowerActivations < 1)
            {
                return;
            }

            GlobalAudioHandler.PlaySoundEffect(SfxSample.Clap);
        }

        private void OnHappinessUnderThreshold()
        {
            if (SettingsManager.Settings.UseCrowdFx.Value == CrowdFxMode.Disabled)
            {
                return;
            }

            ChangeCrowdMuteState(true);
        }

        private void OnHappinessOverThreshold()
        {
            if (SettingsManager.Settings.UseCrowdFx.Value == CrowdFxMode.Disabled)
            {
                return;
            }

            ChangeCrowdMuteState(false);
        }

        private void OnSongFailed()
        {
            // TODO: Play crowd booing sound
        }

        private void ChangeCrowdMuteState(bool muted)
        {
            if (IsCrowdMuted != muted)
            {
                _gameManager.ChangeStemMuteState(SongStem.Crowd, muted, 1.0f);
                IsCrowdMuted = muted;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public void Dispose(bool disposing)
        {
            // If disposing is false, it's too late to do this cleanup, hopefully it gets taken care of by GC
            if (!_disposed && disposing)
            {
                _engineManager.OnHappinessUnderThreshold -= OnHappinessUnderThreshold;
                _engineManager.OnHappinessOverThreshold -= OnHappinessOverThreshold;
                _engineManager.OnSongFailed -= OnSongFailed;
                _gameManager?.BeatEventHandler?.Audio.Unsubscribe(Clap);

                GlobalAudioHandler.StopSoundEffect(_selectedStartSample);
                GlobalAudioHandler.StopSoundEffect(_selectedEndSample);
            }

            _disposed = true;
        }
    }
}
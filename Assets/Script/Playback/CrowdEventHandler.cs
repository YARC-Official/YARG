using System;
using System.Collections.Generic;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Parsing;
using YARG.Gameplay;
using YARG.Gameplay.HUD;
using YARG.Settings;

namespace YARG.Playback
{
    public class CrowdEventHandler : IDisposable
    {
        public CrowdState CrowdState     { get; private set; } = CrowdState.Realtime;
        public ClapState  ClapState { get; private set; } = ClapState.Clap;

        private bool IsCrowdMuted = false;

        private readonly List<CrowdEvent> _events;
        private readonly GameManager      _gameManager;
        private readonly SyncTrack        _syncTrack;
        private readonly EngineManager    _engineManager;

        private SfxSample[] _openSamples = { SfxSample.CrowdOpen1, SfxSample.CrowdOpen2 };
        private SfxSample[] _startSamples = { SfxSample.CrowdStart, SfxSample.CrowdStart2, SfxSample.CrowdStart3 };
        private SfxSample[] _endSamples = { SfxSample.CrowdEnd1, SfxSample.CrowdEnd2 };

        private SfxSample _selectedOpenSample;
        private SfxSample _selectedStartSample;
        private SfxSample _selectedEndSample;

        private int _eventIndex;

        // This is true if we have music start and music end, otherwise we just start with the crowd roar
        private readonly bool _startWithMurmur;

        private bool _startSamplePlayed;
        private bool _endSamplePlayed;
        private bool _disposed;

        private bool _started;
        private CrowdClapScheduler _clapScheduler;

        private readonly double _musicStartTime;
        private readonly double _musicEndTime;

        private bool UseCrowdCheering => SettingsManager.Settings.UseCrowdCheering.Value
            && !GlobalVariables.State.CrowdSfxVenueOverride;

        private bool UseCrowdIdle => SettingsManager.Settings.UseCrowdIdle.Value
            && !GlobalVariables.State.CrowdSfxVenueOverride;

        private bool UseStarPowerClaps => SettingsManager.Settings.UseStarPowerClaps.Value
            && !GlobalVariables.State.CrowdSfxVenueOverride;

        private bool UsePerformanceClaps => SettingsManager.Settings.UsePerformanceClaps.Value
            && !GlobalVariables.State.CrowdSfxVenueOverride;

        public CrowdEventHandler(SongChart chart, GameManager gameManager)
        {
            // Clone the event list so we can modify it if necessary
            _events = new List<CrowdEvent>(chart.CrowdEvents);
            _syncTrack = chart.SyncTrack;
            _gameManager = gameManager;
            _engineManager = gameManager.EngineManager;

            var (musicStart, musicEnd) = chart.GetMusicEvents();

            if (musicStart == null || musicEnd == null)
            {
                _startWithMurmur = false;

                // In this case, just start the crowd sound immediately
                _musicStartTime = -2;
                _musicEndTime = _gameManager.LastNoteTime;
            }
            else
            {
                _startWithMurmur = true;
                _musicStartTime = musicStart.Time;
                _musicEndTime = musicEnd.Time;
            }

        }

        public void SetClapScheduler(CrowdClapScheduler scheduler)
        {
            _clapScheduler = scheduler;
            UpdateClapEnabled();
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            if (UseCrowdIdle || (UseCrowdCheering && _startWithMurmur))
            {
                _selectedOpenSample = _openSamples[UnityEngine.Random.Range(0, _openSamples.Length)];
                GlobalAudioHandler.PlaySoundEffect(_selectedOpenSample, 1.0);
            }

            if (UseCrowdCheering)
            {
                _selectedStartSample = _startSamples[UnityEngine.Random.Range(0, _startSamples.Length)];
                _selectedEndSample = _endSamples[UnityEngine.Random.Range(0, _endSamples.Length)];

                if (!_startWithMurmur)
                {
                    GlobalAudioHandler.PlaySoundEffect(_selectedStartSample, 1.0);
                    _startSamplePlayed = true;
                }
            }

            _started = true;
            UpdateClapEnabled();

            if (SettingsManager.Settings.NoFail.Value == NoFailMode.NoMeter || GlobalVariables.State.IsPractice)
            {
                return;
            }

            if (_gameManager.ReplayInfo == null || GlobalVariables.State.PlayingWithReplay)
            {
                _engineManager.OnSongFailed += OnSongFailed;

                _engineManager.OnHappinessUnderThreshold += OnHappinessUnderThreshold;
                _engineManager.OnHappinessOverThreshold += OnHappinessOverThreshold;
            }
        }

        public void Update(double time)
        {
            if (!_started)
            {
                return;
            }

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

            if (!_startSamplePlayed && time >= _musicStartTime)
            {
                _startSamplePlayed = true;

                if (UseCrowdCheering)
                {
                    if (_startWithMurmur && !UseCrowdIdle)
                    {
                        GlobalAudioHandler.StopSoundEffect(_selectedOpenSample, 1.0);
                    }

                    GlobalAudioHandler.PlaySoundEffect(_selectedStartSample, 0.25);
                }
            }

            if (!_endSamplePlayed && time >= _musicEndTime)
            {
                _endSamplePlayed = true;

                // Play the end sample if it hasn't been played yet
                if (UseCrowdCheering)
                {
                    GlobalAudioHandler.PlaySoundEffect(_selectedEndSample, 0.5);
                }
            }

            UpdateClapEnabled();
        }

        private void UpdateClapEnabled()
        {
            bool starPowerActive = _gameManager.StarPowerActivations > 0;
            bool crowdIsHappy = _engineManager.Happiness >= 1.0f;

            bool clapTriggerActive = (UseStarPowerClaps && starPowerActive) ||
                (UsePerformanceClaps && crowdIsHappy);
            bool shouldEnableClaps = _started && clapTriggerActive;

            _clapScheduler?.SetEnabled(shouldEnableClaps);
        }

        private void OnHappinessUnderThreshold()
        {
            ChangeCrowdMuteState(true);
        }

        private void OnHappinessOverThreshold()
        {
            ChangeCrowdMuteState(false);
        }

        private void OnSongFailed()
        {
            // TODO: Play crowd booing sound
            if (SettingsManager.Settings.NoFail.Value != NoFailMode.Off || _gameManager.IsPractice)
            {
                return;
            }
        }

        private void ChangeCrowdMuteState(bool muted, bool force = false)
        {
            if (IsCrowdMuted != muted || force)
            {
                _gameManager.ChangeStemMuteState(SongStem.Crowd, muted, 1.0f);
                IsCrowdMuted = muted;
            }
        }

        public void UpdateCrowdMuteState(bool force = false)
        {
            ChangeCrowdMuteState(_engineManager.IsCrowdBelowThreshold, force);
        }

        public void StopAllCrowdSounds()
        {
            foreach (var sample in _openSamples)
            {
                GlobalAudioHandler.StopSoundEffect(sample, 2.5);
            }

            foreach (var sample in _startSamples)
            {
                GlobalAudioHandler.StopSoundEffect(sample);
            }

            foreach (var sample in _endSamples)
            {
                GlobalAudioHandler.StopSoundEffect(sample, 1.5);
            }

            GlobalAudioHandler.StopSoundEffect(SfxSample.Chatter);
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
                _clapScheduler?.SetEnabled(false);

                StopAllCrowdSounds();
            }

            _disposed = true;
        }
    }
}

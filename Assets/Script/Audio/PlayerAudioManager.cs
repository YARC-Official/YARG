using System;
using System.Collections.Generic;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Gameplay;
using YARG.Gameplay.Player;
using YARG.Settings;
using static YARG.Core.Audio.SongStem;
using static YARG.Gameplay.Player.PlayerEvent;
using Random = UnityEngine.Random;

namespace YARG.Audio
{
    public class PlayerAudioManager : IDisposable
    {
        private readonly List<AudioHandler>        _handlers;
        private readonly GameManager               _gameManager;
        private readonly SongStem                  _backgroundStem;
        private readonly Dictionary<SongStem, int> _reverbCounts;

        public PlayerAudioManager(GameManager gameManager, SongStem backgroundStem)
        {
            _gameManager = gameManager;
            _backgroundStem = backgroundStem;
            _handlers = new List<AudioHandler>();
            _reverbCounts = new Dictionary<SongStem, int>();
        }

        public void AddPlayer(SongStem stem, SongStem reverbStem, BasePlayer player)
        {
            _handlers.Add(new AudioHandler(stem, reverbStem, player, _gameManager, this, stem != _backgroundStem));
        }

        public void ChangeStemMuteState(SongStem stem, bool muted, float duration = 0.0f)
        {
            _gameManager.ChangeStemMuteState(stem, muted, duration);
        }

        private static AudioFxMode StarPowerFxSetting => SettingsManager.Settings.UseStarpowerFx.Value;

        private void ChangeStemReverbState(SongStem stem, bool reverb)
        {
            int count = _reverbCounts.GetValueOrDefault(stem);
            if (reverb)
            {
                count++;
            }
            else if (count > 0)
            {
                count--;
            }
            _reverbCounts[stem] = count;
            MixerAudioHandler.SetReverbSetting(stem, count > 0);
        }

        public void Dispose()
        {
            foreach (var handler in _handlers)
            {
                handler.Dispose();
            }
            _handlers.Clear();
        }

        private class AudioHandler : IDisposable
        {
            private readonly SongStem           _stem;
            private readonly SongStem           _reverbStem;
            private readonly BasePlayer         _player;
            private readonly GameManager        _gameManager;
            private readonly PlayerAudioManager _audioManager;
            private readonly bool               _isMultiTrack;
            private          bool               _isMuted;
            private          Instrument CurrentInstrument => _player.Player.Profile.CurrentInstrument;
            private          bool IsSeekingReplay => _gameManager.IsSeekingReplay;
            private static   bool AllowOverhitSfx => SettingsManager.Settings.OverstrumAndOverhitSoundEffects.Value;
            private static   bool AllowWhammySetting => SettingsManager.Settings.UseWhammyFx.Value;

            public AudioHandler(SongStem stem, SongStem reverbStem, BasePlayer player, GameManager gameManager, PlayerAudioManager audioManager, bool isMultiTrack)
            {
                _stem = stem;
                _reverbStem = reverbStem;
                _gameManager = gameManager;
                _audioManager = audioManager;
                _isMultiTrack = isMultiTrack;
                _player = player;
                _player.Events += HandlePlayerEvent;
            }

            private void HandlePlayerEvent(PlayerEvent playerEvent)
            {
                YargLogger.LogDebug($"Received event: {playerEvent} for stem {_stem}");
                switch (playerEvent)
                {
                    case StarPowerChanged(var active):
                        OnStarPowerChanged(active);
                        break;
                    case ReplayTimeChanged:
                        OnReplayTimeChanged();
                        break;
                    case VisualsReset:
                        OnVisualsReset();
                        break;
                    case NoteHit:
                        OnNoteHit();
                        break;
                    case NoteMissed(var isComboBreak):
                        OnNoteMissed(isComboBreak);
                        break;
                    case Overhit:
                        OnOverhit();
                        break;
                    case SustainBroken:
                        OnSustainBroken();
                        break;
                    case SustainEnded:
                        OnSustainEnded();
                        break;
                    case StarPowerPhraseHit:
                        OnStarPowerPhraseHit();
                        break;
                    case WhammyDuringSustain(var whammyFactor):
                        OnWhammyDuringSustain(whammyFactor);
                        break;
                }
            }

            private void OnReplayTimeChanged()
            {
                SetMuteState(false);
            }

            private void OnStarPowerPhraseHit()
            {
                if (_gameManager.Paused || IsSeekingReplay)
                {
                    return;
                }

                GlobalAudioHandler.PlaySoundEffect(SfxSample.StarPowerAward);
            }

            private void OnStarPowerChanged(bool active)
            {
                var isSettingOff = StarPowerFxSetting == AudioFxMode.Off;
                var isMultiTrackOnlySetting = StarPowerFxSetting == AudioFxMode.MultitrackOnly;
                bool shouldSkipReverb = isSettingOff || (isMultiTrackOnlySetting && !_isMultiTrack);
                if (shouldSkipReverb)
                {
                    return;
                }

                _audioManager.ChangeStemReverbState(_reverbStem, active);
            }

            private void OnWhammyDuringSustain(float whammyFactor)
            {
                if (!AllowWhammySetting)
                {
                    return;
                }

                ChangeWhammyPitch(whammyFactor);
            }

            private void OnSustainEnded()
            {
                ChangeWhammyPitch(0);
            }

            private void OnSustainBroken()
            {
                SetMuteState(true);
            }

            private void OnVisualsReset()
            {
                SetMuteState(false);
            }

            private void OnNoteMissed(bool isComboBreak)
            {
                if (IsSeekingReplay)
                {
                    return;
                }

                SetMuteState(true);

                if (isComboBreak)
                {
                    PlayMissSfx();
                }
            }

            private void OnOverhit()
            {
                var shouldPlaySfx = !IsSeekingReplay && CurrentInstrument.IsFiveFret() && AllowOverhitSfx;
                if (shouldPlaySfx)
                {
                    PlayOverstrumSfx();
                }
            }

            private void OnNoteHit()
            {
                if (IsSeekingReplay)
                {
                    return;
                }
                SetMuteState(false);
            }

            private void SetMuteState(bool muted)
            {
                if (_stem == Vocals || _isMuted == muted)
                {
                    return;
                }

                _audioManager.ChangeStemMuteState(_stem, muted);
                _isMuted = muted;
            }

            private void ChangeWhammyPitch(float percent)
            {
                // Ignore whammy on the background stem to avoid pitch-bending the full mix.
                if (!_isMultiTrack)
                {
                    return;
                }
                MixerAudioHandler.SetWhammyPitchSetting(_stem, percent);
            }

            private void PlayOverstrumSfx()
            {
                const int min = (int) SfxSample.Overstrum1;
                const int max = (int) SfxSample.Overstrum4;
                var randomOverstrum = (SfxSample) Random.Range(min, max + 1);
                GlobalAudioHandler.PlaySoundEffect(randomOverstrum);
            }

            private void PlayMissSfx()
            {
                GlobalAudioHandler.PlaySoundEffect(SfxSample.NoteMiss);
            }

            public void Dispose()
            {
                _player.Events -= HandlePlayerEvent;
            }
        }
    }
}

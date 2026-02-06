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
        private readonly List<AudioHandler> _handlers;
        private readonly GameManager        _gameManager;
        private static   bool AllowOverhitSfx => SettingsManager.Settings.OverstrumAndOverhitSoundEffects.Value;

        public PlayerAudioManager(GameManager gameManager)
        {
            _gameManager = gameManager;
            _handlers = new List<AudioHandler>();
        }

        public void AddPlayer(SongStem stem, BasePlayer player)
        {
            _handlers.Add(new AudioHandler(stem, player, _gameManager));
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
            private readonly SongStem    _stem;
            private readonly BasePlayer  _player;
            private readonly GameManager _gameManager;
            private          bool        _isMuted;
            private Instrument CurrentInstrument => _player.Player.Profile.CurrentInstrument;
            private bool IsSeekingReplay => _gameManager.IsSeekingReplay;

            public AudioHandler(SongStem stem, BasePlayer player, GameManager gameManager)
            {
                _stem = stem;
                _gameManager = gameManager;
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
                    case NoteMissed(var lastCombo):
                        OnNoteMissed(lastCombo);
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
                var reverbStem = SongStem.Song;
                if (_stem is Drums or Bass or Rhythm or Guitar)
                {
                    reverbStem = _stem;
                }
                _gameManager.ChangeStemReverbState(reverbStem, active);
            }

            private void OnWhammyDuringSustain(float whammyFactor)
            {
                _gameManager.ChangeStemWhammyPitch(_stem, whammyFactor);
            }

            private void OnSustainEnded()
            {
                _gameManager.ChangeStemWhammyPitch(_stem, 0);
            }

            private void OnSustainBroken()
            {
                SetMuteState(true);
            }

            private void OnVisualsReset()
            {
                SetMuteState(false);
            }

            private void OnNoteMissed(int lastCombo)
            {
                if (IsSeekingReplay)
                {
                    return;
                }

                SetMuteState(true);

                int comboBreakThreshold = _stem == Vocals ? 2 : BasePlayer.COMBO_BREAK_THRESHOLD;
                if (lastCombo >= comboBreakThreshold)
                {
                    GlobalAudioHandler.PlaySoundEffect(SfxSample.NoteMiss);
                }
            }

            private void OnOverhit()
            {
                if (IsSeekingReplay)
                {
                    return;
                }

                if (!CurrentInstrument.IsFiveFret())
                {
                    return;
                }

                if (!AllowOverhitSfx)
                {
                    return;
                }

                const int min = (int) SfxSample.Overstrum1;
                const int max = (int) SfxSample.Overstrum4;
                var randomOverstrum = (SfxSample) Random.Range(min, max + 1);
                GlobalAudioHandler.PlaySoundEffect(randomOverstrum);
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
                _gameManager.ChangeStemMuteState(_stem, muted);
                _isMuted = muted;
            }

            public void Dispose()
            {
                _player.Events -= HandlePlayerEvent;
            }
        }
    }
}
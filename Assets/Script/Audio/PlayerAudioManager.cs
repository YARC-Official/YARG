using System;
using System.Collections.Generic;
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
        private readonly        List<AudioHandler> _handlers;
        private readonly        GameManager        _gameManager;
        private static bool AllowOverhitSfx => SettingsManager.Settings.OverstrumAndOverhitSoundEffects.Value;

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
                    case NoteMissed:
                        OnNoteMissed();
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
                    case WhammyDuringSustain(var whammyFactor):
                        OnWhammyDuringSustain(whammyFactor);
                        break;
                }
            }

            private void OnReplayTimeChanged()
            {
                SetMuteState(false);
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

            private void OnNoteMissed()
            {
                if (!_gameManager.IsSeekingReplay)
                {
                    SetMuteState(true);
                }
            }

            private void OnOverhit()
            {
                if (_gameManager.IsSeekingReplay)
                {
                    return;
                }

                if (!AllowOverhitSfx)
                {
                    return;
                }

                const int MIN = (int) SfxSample.Overstrum1;
                const int MAX = (int) SfxSample.Overstrum4;
                var randomOverstrum = (SfxSample) Random.Range(MIN, MAX + 1);
                GlobalAudioHandler.PlaySoundEffect(randomOverstrum);
            }

            private void OnNoteHit()
            {
                if (!_gameManager.IsSeekingReplay)
                {
                    SetMuteState(false);
                }
            }

            private void SetMuteState(bool muted)
            {
                if (_stem == Vocals || _isMuted == muted)
                {
                    return;
                }


                YargLogger.LogDebug($"SETTING MUTE STATE {muted}");
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
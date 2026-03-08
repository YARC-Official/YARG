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
        private class PlayerContext
        {
            public StemState           StemState;
            public StemState           ReverbStemState;
            public BasePlayer          Player;
            public bool                IsMultiTrack;
            public bool                IsMuted;
            public Action<PlayerEvent> EventHandler;
        }

        private readonly List<PlayerContext>             _contexts;
        private readonly GameManager                     _gameManager;
        private readonly SongStem                        _backgroundStem;
        private readonly Dictionary<SongStem, StemState> _stemStates;
        private readonly HashSet<SongStem>               _mixerStems;
        private          bool                            IsMultiTrack => _mixerStems.Count > 1;

        private static AudioFxMode MuteOnMissSetting  => SettingsManager.Settings.MuteOnMiss.Value;
        private static AudioFxMode StarPowerFxSetting => SettingsManager.Settings.UseStarpowerFx.Value;
        private static bool        AllowOverhitSfx    => SettingsManager.Settings.OverstrumAndOverhitSoundEffects.Value;
        private static bool        AllowWhammySetting => SettingsManager.Settings.UseWhammyFx.Value;

        public PlayerAudioManager(GameManager gameManager, SongStem backgroundStem, IEnumerable<SongStem> mixerStems)
        {
            _gameManager = gameManager;
            _backgroundStem = backgroundStem;
            _contexts = new List<PlayerContext>();
            _stemStates = new Dictionary<SongStem, StemState>();
            _mixerStems = new HashSet<SongStem>(mixerStems);
        }

        public void AddPlayer(BasePlayer player)
        {
            var instrumentStem = player.Player.Profile.CurrentInstrument.ToSongStem();
            if (instrumentStem == Bass && !_mixerStems.Contains(Bass))
            {
                instrumentStem = Rhythm;
            }
            var reverbStem = _mixerStems.Contains(instrumentStem) ? instrumentStem : _backgroundStem;
            AddPlayer(instrumentStem, reverbStem, player);
        }

        public void AddPlayer(SongStem stem, SongStem reverbStem, BasePlayer player)
        {
            var context = new PlayerContext
            {
                StemState = GetOrCreateStemState(stem),
                ReverbStemState = GetOrCreateStemState(reverbStem),
                Player = player,
                IsMultiTrack = stem != _backgroundStem
            };

            context.EventHandler = (playerEvent) => HandlePlayerEvent(context, playerEvent);
            player.Events += context.EventHandler;
            _contexts.Add(context);

            RegisterStemForPlayer(stem);
        }

        private void RegisterStemForPlayer(SongStem stem)
        {
            var state = GetOrCreateStemState(stem);
            if (IsMultiTrack && _mixerStems.Contains(stem))
            {
                state.RegisterTrack();
            }
            else if (_mixerStems.Contains(_backgroundStem))
            {
                state.RegisterBackground();
            }
        }

        private StemState GetOrCreateStemState(SongStem stem)
        {
            if (!_stemStates.TryGetValue(stem, out var state))
            {
                state = new StemState(stem);
                _stemStates[stem] = state;
            }

            return state;
        }

        private void ChangeStemMuteState(StemState state, bool muted)
        {
            if (MuteOnMissSetting == AudioFxMode.Off
                || (MuteOnMissSetting == AudioFxMode.MultitrackOnly && !IsMultiTrack))
            {
                return;
            }

            double volume = state.SetMute(muted);
            MixerAudioHandler.SetVolumeSetting(state.Stem, volume);
        }

        private void ChangeStemReverbState(StemState state, bool reverb)
        {
            var hasReverb = state.SetReverb(reverb);
            MixerAudioHandler.SetReverbSetting(state.Stem, hasReverb);
        }

        private void HandlePlayerEvent(PlayerContext context, PlayerEvent playerEvent)
        {
            YargLogger.LogDebug($"Received event: {playerEvent} for stem {context.StemState.Stem}");
            switch (playerEvent)
            {
                case StarPowerChanged(var active):
                    OnStarPowerChanged(context, active);
                    break;
                case ReplayTimeChanged:
                    OnReplayTimeChanged(context);
                    break;
                case VisualsReset:
                    OnVisualsReset(context);
                    break;
                case NoteHit:
                    OnNoteHit(context);
                    break;
                case NoteMissed(var isComboBreak):
                    OnNoteMissed(context, isComboBreak);
                    break;
                case Overhit:
                    OnOverhit(context);
                    break;
                case SustainBroken:
                    OnSustainBroken(context);
                    break;
                case SustainEnded:
                    OnSustainEnded(context);
                    break;
                case StarPowerPhraseHit:
                    OnStarPowerPhraseHit();
                    break;
                case WhammyDuringSustain(var whammyFactor):
                    OnWhammyDuringSustain(context, whammyFactor);
                    break;
            }
        }

        private void OnReplayTimeChanged(PlayerContext context)
        {
            SetMuteState(context, false);
        }

        private void OnStarPowerPhraseHit()
        {
            if (_gameManager.Paused || _gameManager.IsSeekingReplay)
            {
                return;
            }

            GlobalAudioHandler.PlaySoundEffect(SfxSample.StarPowerAward);
        }

        private void OnStarPowerChanged(PlayerContext context, bool active)
        {
            var isSettingOff = StarPowerFxSetting == AudioFxMode.Off;
            var isMultiTrackOnlySetting = StarPowerFxSetting == AudioFxMode.MultitrackOnly;
            bool shouldSkipReverb = isSettingOff || (isMultiTrackOnlySetting && !context.IsMultiTrack);
            if (shouldSkipReverb)
            {
                return;
            }

            ChangeStemReverbState(context.ReverbStemState, active);
        }

        private void OnWhammyDuringSustain(PlayerContext context, float whammyFactor)
        {
            if (!AllowWhammySetting)
            {
                return;
            }

            ChangeWhammyPitch(context, whammyFactor);
        }

        private void OnSustainEnded(PlayerContext context)
        {
            ChangeWhammyPitch(context, 0);
        }

        private void OnSustainBroken(PlayerContext context)
        {
            SetMuteState(context, true);
        }

        private void OnVisualsReset(PlayerContext context)
        {
            SetMuteState(context, false);
        }

        private void OnNoteMissed(PlayerContext context, bool isComboBreak)
        {
            if (_gameManager.IsSeekingReplay)
            {
                return;
            }

            SetMuteState(context, true);

            if (isComboBreak)
            {
                PlayMissSfx();
            }
        }

        private void OnOverhit(PlayerContext context)
        {
            var shouldPlaySfx = !_gameManager.IsSeekingReplay &&
                context.Player.Player.Profile.CurrentInstrument.IsFiveFret() && AllowOverhitSfx;
            if (shouldPlaySfx)
            {
                PlayOverstrumSfx();
            }
        }

        private void OnNoteHit(PlayerContext context)
        {
            if (_gameManager.IsSeekingReplay)
            {
                return;
            }

            SetMuteState(context, false);
        }

        private void SetMuteState(PlayerContext context, bool muted)
        {
            if (context.StemState.Stem == Vocals || context.IsMuted == muted)
            {
                return;
            }

            ChangeStemMuteState(context.StemState, muted);
            context.IsMuted = muted;
        }

        private static void ChangeWhammyPitch(PlayerContext context, float percent)
        {
            // Ignore whammy on the background stem to avoid pitch-bending the full mix.
            if (!context.IsMultiTrack)
            {
                return;
            }

            MixerAudioHandler.SetWhammyPitchSetting(context.StemState.Stem, percent);
        }

        private static void PlayOverstrumSfx()
        {
            const int min = (int) SfxSample.Overstrum1;
            const int max = (int) SfxSample.Overstrum4;
            var randomOverstrum = (SfxSample) Random.Range(min, max + 1);
            GlobalAudioHandler.PlaySoundEffect(randomOverstrum);
        }

        private static void PlayMissSfx()
        {
            GlobalAudioHandler.PlaySoundEffect(SfxSample.NoteMiss);
        }

        public void Dispose()
        {
            foreach (var context in _contexts)
            {
                context.Player.Events -= context.EventHandler;
            }

            _contexts.Clear();

            // Restore player-owned stems to current user settings
            foreach (var state in _stemStates.Values)
            {
                MixerAudioHandler.SetVolumeSetting(state.Stem, state.Volume);
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Replays;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Player;
using YARG.Settings;
using YARG.Themes;

namespace YARG.Gameplay.Player
{
    public class DrumsPlayer : TrackPlayer<DrumsEngine, DrumNote>
    {
        public DrumsEngineParameters EngineParams { get; private set; }

        [Header("Drums Specific")]
        [SerializeField]
        private bool _fiveLaneMode;
        [SerializeField]
        private FretArray _fretArray;
        [SerializeField]
        private KickFretFlash _kickFretFlash;

        public override bool ShouldUpdateInputsOnResume => false;

        public override float[] StarMultiplierThresholds { get; protected set; } =
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.29f
        };

        public override int[] StarScoreThresholds { get; protected set; }

        private int[] _drumSoundEffectRoundRobin = new int[8];
        private float _drumSoundEffectAccentThreshold;

        public override void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView, StemMixer mixer,
            int? currentHighScore)
        {
            // Before we do anything, see if we're in five lane mode or not
            _fiveLaneMode = player.Profile.CurrentInstrument == Instrument.FiveLaneDrums;
            base.Initialize(index, player, chart, trackView, mixer, currentHighScore);
        }

        protected override InstrumentDifficulty<DrumNote> GetNotes(SongChart chart)
        {
            var track = chart.GetDrumsTrack(Player.Profile.CurrentInstrument).Clone();
            var instrumentDifficulty = track.GetDifficulty(Player.Profile.CurrentDifficulty);
            instrumentDifficulty.SetDrumActivationFlags(Player.Profile.StarPowerActivationType);
            return instrumentDifficulty;
        }

        protected override DrumsEngine CreateEngine()
        {
            var mode = Player.Profile.CurrentInstrument switch
            {
                Instrument.ProDrums      => DrumsEngineParameters.DrumMode.ProFourLane,
                Instrument.FourLaneDrums => DrumsEngineParameters.DrumMode.NonProFourLane,
                Instrument.FiveLaneDrums => DrumsEngineParameters.DrumMode.FiveLane,
                _                        => throw new Exception("Unreachable.")
            };

            if (!Player.IsReplay)
            {
                // Create the engine params from the engine preset
                EngineParams = Player.EnginePreset.Drums.Create(StarMultiplierThresholds, mode);
            }
            else
            {
                // Otherwise, get from the replay
                EngineParams = (DrumsEngineParameters) Player.EngineParameterOverride;
            }

            var engine = new YargDrumsEngine(NoteTrack, SyncTrack, EngineParams, Player.Profile.IsBot);
            EngineContainer = GameManager.EngineManager.Register(engine, NoteTrack.Instrument, Chart);

            HitWindow = EngineParams.HitWindow;

            // Calculating drum sound effect accent threshold based on the engine's ghost velocity threshold
            _drumSoundEffectAccentThreshold = EngineParams.VelocityThreshold * 2;
            if (_drumSoundEffectAccentThreshold > 0.8f)
            {
                _drumSoundEffectAccentThreshold = EngineParams.VelocityThreshold + ((1 - EngineParams.VelocityThreshold) / 2);
            }

            engine.OnNoteHit += OnNoteHit;
            engine.OnNoteMissed += OnNoteMissed;
            engine.OnOverhit += OnOverhit;

            engine.OnSoloStart += OnSoloStart;
            engine.OnSoloEnd += OnSoloEnd;

            engine.OnStarPowerPhraseHit += OnStarPowerPhraseHit;
            engine.OnStarPowerStatus += OnStarPowerStatus;

            engine.OnCountdownChange += OnCountdownChange;

            engine.OnPadHit += OnPadHit;

            return engine;
        }

        protected override void FinishInitialization()
        {
            StarScoreThresholds = PopulateStarScoreThresholds(StarMultiplierThresholds, Engine.BaseScore);

            // Get the proper info for four/five lane
            ColorProfile.IFretColorProvider colors = !_fiveLaneMode
                ? Player.ColorProfile.FourLaneDrums
                : Player.ColorProfile.FiveLaneDrums;

            if (_fiveLaneMode)
            {
                _fretArray.FretCount = 5;
            }
            else if (Player.Profile.SplitProTomsAndCymbals && EngineParams.Mode == DrumsEngineParameters.DrumMode.ProFourLane)
            {
                _fretArray.FretCount = 7;
            }
            else
            {
                _fretArray.FretCount = 4;
            }

            _fretArray.Initialize(
                Player.ThemePreset,
                Player.Profile.GameMode,
                colors,
                Player.Profile.LeftyFlip,
                Player.Profile.CurrentInstrument is Instrument.ProDrums && Player.Profile.SplitProTomsAndCymbals,
                ShouldSwapSnareAndHiHat(),
                ShouldSwapCrashAndRide()
            );

            // Particle 0 is always kick fret
            _kickFretFlash.Initialize(colors.GetParticleColor(0).ToUnityColor());

            // Set up drum fill lead-ups
            SetDrumFillEffects();

            base.FinishInitialization();
        }

        private int GetFillLaneForSplitView(int rightmostPad)
        {
            return rightmostPad switch
            {
                0 => 0,
                1 => ShouldSwapSnareAndHiHat() ? 2 : 1,
                2 => 3,
                3 => 5,
                4 => 7,
                5 => ShouldSwapSnareAndHiHat() ? 1 : 2,
                6 => ShouldSwapCrashAndRide() ? 6 : 4,
                7 => ShouldSwapCrashAndRide() ? 4 : 6,
                _ => 0,
            };
        }

        private void SetDrumFillEffects()
        {
            int checkpoint = 0;
            var pairedFillIndexes = new HashSet<int>();

            // Find activation gems
            foreach (var chord in Notes)
            {
                DrumNote rightmostNote = chord.ParentOrSelf;
                bool foundStarpower = false;

                // Check for SP activation note
                foreach (var note in chord.AllNotes)
                {
                    if (note.IsStarPowerActivator)
                    {
                        if (note.Pad > rightmostNote.Pad)
                        {
                            rightmostNote = note;
                        }
                        foundStarpower = true;
                    }
                }

                if (!foundStarpower)
                {
                    continue;
                }

                int fillLane = rightmostNote.Pad;

                // Convert pad to lane for pro
                if (Player.Profile.CurrentInstrument == Instrument.ProDrums)
                {
                    if (Player.Profile.SplitProTomsAndCymbals)
                    {
                        fillLane = GetFillLaneForSplitView(fillLane);
                    }
                    else if (fillLane > 4)
                    {
                        fillLane -= 3;
                    }
                }

                int candidateIndex = -1;

                // Find the drum fill immediately before this note
                for (var i = checkpoint; i < _trackEffects.Count; i++)
                {
                    if (_trackEffects[i].EffectType != TrackEffectType.DrumFill)
                    {
                        continue;
                    }

                    var effect = _trackEffects[i];

                    if (effect.TimeEnd <= chord.Time)
                    {
                        candidateIndex = i;
                    }
                    else
                    {
                        break;
                    }
                }

                if (candidateIndex != -1)
                {
                    _trackEffects[candidateIndex].FillLane = fillLane;
                    _trackEffects[candidateIndex].TotalLanes = _fretArray.FretCount;
                    pairedFillIndexes.Add(candidateIndex);
                    checkpoint = candidateIndex;
                }
            }

            // Remove fills that are not paired with a note
            for (var i = _trackEffects.Count - 1; i >= 0; i--)
            {
                if (_trackEffects[i].EffectType == TrackEffectType.DrumFill && !pairedFillIndexes.Contains(i))
                {
                    _trackEffects.RemoveAt(i);
                }
            }
        }

        public override void SetStemMuteState(bool muted)
        {
            if (IsStemMuted != muted)
            {
                GameManager.ChangeStemMuteState(SongStem.Drums, muted);
                IsStemMuted = muted;
            }
        }

        public override void SetStarPowerFX(bool active)
        {
            GameManager.ChangeStemReverbState(SongStem.Drums, active);
        }

        protected override void ResetVisuals()
        {
            base.ResetVisuals();

            _fretArray.ResetAll();
        }

        protected override void InitializeSpawnedNote(IPoolable poolable, DrumNote note)
        {
            ((DrumsNoteElement) poolable).NoteRef = note;
        }

        protected override void OnNoteHit(int index, DrumNote note)
        {
            base.OnNoteHit(index, note);

            // Remember that drums treat each note separately

            (NotePool.GetByKey(note) as DrumsNoteElement)?.HitNote();

            // Four and five lane drums have the same kick value
            if (note.Pad != (int) FourLaneDrumPad.Kick)
            {
                int fret;
                if (!_fiveLaneMode)
                {
                    if (Player.Profile.SplitProTomsAndCymbals && EngineParams.Mode == DrumsEngineParameters.DrumMode.ProFourLane)
                    {
                        fret = (FourLaneDrumPad) note.Pad switch
                        {
                            FourLaneDrumPad.RedDrum      => 0,
                            FourLaneDrumPad.YellowCymbal => 1,
                            FourLaneDrumPad.YellowDrum   => 2,
                            FourLaneDrumPad.BlueCymbal   => 3,
                            FourLaneDrumPad.BlueDrum     => 4,
                            FourLaneDrumPad.GreenCymbal  => 5,
                            FourLaneDrumPad.GreenDrum    => 6,
                            _ => -1
                        };
                    }
                    else
                    {
                        fret = (FourLaneDrumPad) note.Pad switch
                        {
                            FourLaneDrumPad.RedDrum                                    => 0,
                            FourLaneDrumPad.YellowDrum or FourLaneDrumPad.YellowCymbal => 1,
                            FourLaneDrumPad.BlueDrum or FourLaneDrumPad.BlueCymbal     => 2,
                            FourLaneDrumPad.GreenDrum or FourLaneDrumPad.GreenCymbal   => 3,
                            _                                                          => -1
                        };
                    }
                }
                else
                {
                    fret = (FiveLaneDrumPad) note.Pad switch
                    {
                        FiveLaneDrumPad.Red    => 0,
                        FiveLaneDrumPad.Yellow => 1,
                        FiveLaneDrumPad.Blue   => 2,
                        FiveLaneDrumPad.Orange => 3,
                        FiveLaneDrumPad.Green  => 4,
                        _                      => -1
                    };
                }

                _fretArray.PlayHitAnimation(fret);
            }
            else
            {
                _kickFretFlash.PlayHitAnimation();
                _fretArray.PlayKickFretAnimation();
                CameraPositioner.Bounce();
            }
        }

        protected override void OnNoteMissed(int index, DrumNote note)
        {
            base.OnNoteMissed(index, note);

            // Remember that drums treat each note separately

            (NotePool.GetByKey(note) as DrumsNoteElement)?.MissNote();
        }

        protected override void OnStarPowerPhraseHit()
        {
            base.OnStarPowerPhraseHit();

            foreach (var note in NotePool.AllSpawned)
            {
                (note as DrumsNoteElement)?.OnStarPowerUpdated();
            }
        }

        protected override void OnStarPowerStatus(bool status)
        {
            base.OnStarPowerStatus(status);

            foreach (var note in NotePool.AllSpawned)
            {
                (note as DrumsNoteElement)?.OnStarPowerUpdated();
            }
        }

        private void OnPadHit(DrumsAction action, bool wasNoteHit, float velocity)
        {
            // Skip if a note was hit, because we have different logic for that below
            if (wasNoteHit)
            {
                // If AODSFX is turned on and a note was hit, Play the drum sfx. Without this, drum sfx will only play on misses.
                if (SettingsManager.Settings.AlwaysOnDrumSFX.Value)
                {
                    PlayDrumSoundEffect(action, velocity);
                }
                return;
            }

            // Choose the correct fret
            int fret;
            if (!_fiveLaneMode)
            {
                if (Player.Profile.SplitProTomsAndCymbals && Player.Profile.CurrentInstrument == Instrument.ProDrums)
                {
                    fret = action switch
                    {
                        DrumsAction.Kick         => 0,
                        DrumsAction.RedDrum      => 1,
                        DrumsAction.YellowCymbal => 2,
                        DrumsAction.YellowDrum   => 3,
                        DrumsAction.BlueCymbal   => 4,
                        DrumsAction.BlueDrum     => 5,
                        DrumsAction.GreenCymbal  => 6,
                        DrumsAction.GreenDrum    => 7,
                        _                        => -1
                    };
                }
                else
                {
                    fret = action switch
                    {
                        DrumsAction.Kick                                   => 0,
                        DrumsAction.RedDrum                                => 1,
                        DrumsAction.YellowDrum or DrumsAction.YellowCymbal => 2,
                        DrumsAction.BlueDrum or DrumsAction.BlueCymbal     => 3,
                        DrumsAction.GreenDrum or DrumsAction.GreenCymbal   => 4,
                        _                                                  => -1
                    };
                }
            }
            else
            {
                fret = action switch
                {
                    DrumsAction.Kick         => 0,
                    DrumsAction.RedDrum      => 1,
                    DrumsAction.YellowCymbal => 2,
                    DrumsAction.BlueDrum     => 3,
                    DrumsAction.OrangeCymbal => 4,
                    DrumsAction.GreenDrum    => 5,
                    _                        => -1
                };
            }

            bool isDrumFreestyle = IsDrumFreestyle();

            // Figure out wether its a drum freestyle or if AODSFX is enabled
            if (SettingsManager.Settings.AlwaysOnDrumSFX.Value || isDrumFreestyle)
            {
                // Play drum sound effect
                PlayDrumSoundEffect(action, velocity);
            }

            // Skip if no animation
            if (fret == -1)
            {
                return;
            }

            if (fret != 0)
            {
                if (isDrumFreestyle)
                {
                    _fretArray.PlayHitAnimation(fret - 1);
                }
                else
                {
                    _fretArray.PlayMissAnimation(fret - 1);
                }
            }
            else
            {
                _fretArray.PlayKickFretAnimation();
                if (isDrumFreestyle)
                {
                    _kickFretFlash.PlayHitAnimation();
                    CameraPositioner.Bounce();
                }
            }
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            return false;
        }

        private void PlayDrumSoundEffect(DrumsAction action, float velocity)
        {
            int actionIndex = (int) action;
            double sampleVolume = velocity;

            // Define sample
            int sampleIndex = (int) DrumSfxSample.Vel0Pad0Smp0;
            if (velocity > _drumSoundEffectAccentThreshold)
            {
                sampleIndex = (int) DrumSfxSample.Vel2Pad0Smp0;
            }
            // VelocityThreshold refers to the maximum ghost input velocity
            else if (velocity > EngineParams.VelocityThreshold)
            {
                sampleIndex = (int) DrumSfxSample.Vel1Pad0Smp0;
                // This division is normalizing the volume using _drumSoundEffectAccentThreshold as pseudo "1"
                sampleVolume = velocity / _drumSoundEffectAccentThreshold;
            }
            else
            {
                // This division is normalizing the volume using EngineParams.VelocityThreshold as pseudo "1"
                sampleVolume = velocity / EngineParams.VelocityThreshold;
            }
            sampleIndex += (actionIndex * DrumSampleChannel.ROUND_ROBIN_MAX_INDEX) + _drumSoundEffectRoundRobin[actionIndex];

            // Play Sample
            GlobalAudioHandler.PlayDrumSoundEffect((DrumSfxSample) sampleIndex, sampleVolume);

            // Adjust round-robin
            _drumSoundEffectRoundRobin[actionIndex] += 1;
            if (_drumSoundEffectRoundRobin[actionIndex] == DrumSampleChannel.ROUND_ROBIN_MAX_INDEX)
            {
                _drumSoundEffectRoundRobin[actionIndex] = 0;
            }
        }

        private bool IsDrumFreestyle()
        {
            return Engine.NoteIndex == 0 || // Can freestyle before first note is hit/missed
                Engine.NoteIndex >= Notes.Count || // Can freestyle after last note
                Engine.IsWaitCountdownActive; // Can freestyle during WaitCountdown
            // TODO: add drum fill / BRE conditions
        }

        public override (ReplayFrame Frame, ReplayStats Stats) ConstructReplayData()
        {
            var frame = new ReplayFrame(Player.Profile, EngineParams, Engine.EngineStats, ReplayInputs.ToArray());
            return (frame, Engine.EngineStats.ConstructReplayStats(Player.Profile.Name));
        }

        private bool ShouldSwapSnareAndHiHat()
        {
            if (
                (Player.Profile.GameMode is GameMode.FiveLaneDrums) ||
                (Player.Profile.CurrentInstrument is Instrument.ProDrums && Player.Profile.SplitProTomsAndCymbals)
            )
            {
                return Player.Profile.SwapSnareAndHiHat;
            }

            return false;
        }

        private bool ShouldSwapCrashAndRide() =>
            Player.Profile.CurrentInstrument is Instrument.ProDrums &&
            Player.Profile.SplitProTomsAndCymbals &&
            Player.Profile.SwapCrashAndRide;
    }
}

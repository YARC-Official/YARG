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
        private const float DRUM_PAD_FLASH_HOLD_DURATION = 0.2f;

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

        private Dictionary<int, float> _fretToLastPressedTimeDelta = new Dictionary<int, float>();

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
                _fiveLaneMode ? VisualStyle.FiveLaneDrums : VisualStyle.FourLaneDrums,
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

            // Initialize hit timestamps
            InitializeHitTimes();

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

            AnimateFret(note.Pad);
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
            // Update last hit times for fret flashing animation
            if (action is not DrumsAction.Kick)
            {
                ZeroOutHitTime(action);
            }

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

            bool isDrumFreestyle = IsDrumFreestyle();

            // Figure out wether its a drum freestyle or if AODSFX is enabled
            if (SettingsManager.Settings.AlwaysOnDrumSFX.Value || isDrumFreestyle)
            {
                // Play drum sound effect
                PlayDrumSoundEffect(action, velocity);
            }

            if (action is not DrumsAction.Kick)
            {
                if (isDrumFreestyle)
                {
                    AnimateAction(action);
                }
                else
                {
                    int fret = GetFret(action);
                    _fretArray.PlayMissAnimation(fret);
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

        protected override void UpdateVisuals(double visualTime)
        {
            base.UpdateVisuals(visualTime);
            UpdateHitTimes();
            UpdateFretArray();
        }

        private void InitializeHitTimes()
        {
            for (int fret = 0; fret < _fretArray.FretCount; fret++)
            {
                _fretToLastPressedTimeDelta[fret] = float.MaxValue;
            }
        }

        // i.e., flash this fret by making it seem pressed
        private void ZeroOutHitTime(DrumsAction action)
        {
            int fret = GetFret(action);
            _fretToLastPressedTimeDelta[fret] = 0f;
        }

        private void UpdateHitTimes()
        {
            for (int fret = 0; fret < _fretArray.FretCount; fret++)
            {
                _fretToLastPressedTimeDelta[fret] += Time.deltaTime;
            }
        }

        private void UpdateFretArray()
        {
            for (int fret = 0; fret < _fretArray.FretCount; fret++)
            {
                _fretArray.SetPressed(fret, _fretToLastPressedTimeDelta[fret] < DRUM_PAD_FLASH_HOLD_DURATION);
            }
        }

        private void AnimateAction(DrumsAction action)
        {
            // Refers to the lane where 0 is red
            int fret = GetFret(action);

            if (_fiveLaneMode)
            {
                // Only use cymbal animation if the cymbal gems are being used
                if (Player.Profile.UseCymbalModels && action is DrumsAction.YellowCymbal or DrumsAction.OrangeCymbal)
                {
                    _fretArray.PlayCymbalHitAnimation(fret);
                }
                else
                {
                    _fretArray.PlayHitAnimation(fret);
                }

                return;
            }

            // Can technically merge this condition with the above, but it's more readable like this
            if (action is DrumsAction.YellowCymbal or DrumsAction.BlueCymbal or DrumsAction.GreenCymbal)
            {
                _fretArray.PlayCymbalHitAnimation(fret);
            }
            else
            {
                _fretArray.PlayHitAnimation(fret);
            }
        }

        private void AnimateFret(int pad)
        {
            // Four and five lane drums have the same kick value
            if (pad == (int) FourLaneDrumPad.Kick)
            {
                _kickFretFlash.PlayHitAnimation();
                _fretArray.PlayKickFretAnimation();
                CameraPositioner.Bounce();
                return;
            }

            // Must be a pad or cymbal
            int fret = GetFret(pad);

            if (_fiveLaneMode)
            {
                // Only use cymbal animation if the cymbal gems are being used
                if (Player.Profile.UseCymbalModels && (FiveLaneDrumPad) pad
                    is FiveLaneDrumPad.Yellow
                    or FiveLaneDrumPad.Orange)
                {
                    _fretArray.PlayCymbalHitAnimation(fret);
                }
                else
                {
                    _fretArray.PlayHitAnimation(fret);
                }

                return;
            }

            // Can technically merge this condition with the above, but it's more readable like this
            if ((FourLaneDrumPad) pad
                is FourLaneDrumPad.YellowCymbal
                or FourLaneDrumPad.BlueCymbal
                or FourLaneDrumPad.GreenCymbal)
            {
                _fretArray.PlayCymbalHitAnimation(fret);
            }
            else
            {
                _fretArray.PlayHitAnimation(fret);
            }
        }

        private int GetFret(DrumsAction action)
        {
            if (_fiveLaneMode)
            {
                return GetFiveLaneFret(action);
            }

            if (Player.Profile.SplitProTomsAndCymbals && Player.Profile.CurrentInstrument == Instrument.ProDrums)
            {
                return GetSplitFret(action);
            }

            return GetFourLaneFret(action);
        }

        private static int GetFourLaneFret(DrumsAction action)
        {
            return action switch
            {
                DrumsAction.RedDrum                                => 0,
                DrumsAction.YellowDrum or DrumsAction.YellowCymbal => 1,
                DrumsAction.BlueDrum or DrumsAction.BlueCymbal     => 2,
                DrumsAction.GreenDrum or DrumsAction.GreenCymbal   => 3,
                _                                                  => -1,
            };
        }

        private static int GetFiveLaneFret(DrumsAction action)
        {
            return action switch
            {
                DrumsAction.RedDrum      => 0,
                DrumsAction.YellowCymbal => 1,
                DrumsAction.BlueDrum     => 2,
                DrumsAction.OrangeCymbal => 3,
                DrumsAction.GreenDrum    => 4,
                _                        => -1,
            };
        }

        private static int GetSplitFret(DrumsAction action)
        {
            return action switch
            {
                DrumsAction.RedDrum      => 0,
                DrumsAction.YellowCymbal => 1,
                DrumsAction.YellowDrum   => 2,
                DrumsAction.BlueCymbal   => 3,
                DrumsAction.BlueDrum     => 4,
                DrumsAction.GreenCymbal  => 5,
                DrumsAction.GreenDrum    => 6,
                _                        => -1,
            };
        }

        private int GetFret(int pad)
        {
            if (_fiveLaneMode)
            {
                return GetFiveLaneFret(pad);
            }

            if (Player.Profile.SplitProTomsAndCymbals
                && EngineParams.Mode == DrumsEngineParameters.DrumMode.ProFourLane)
            {
                return GetSplitFret(pad);
            }

            return GetFourLaneFret(pad);
        }

        private static int GetFourLaneFret(int pad)
        {
            return (FourLaneDrumPad) pad switch
            {
                FourLaneDrumPad.RedDrum                                    => 0,
                FourLaneDrumPad.YellowDrum or FourLaneDrumPad.YellowCymbal => 1,
                FourLaneDrumPad.BlueDrum or FourLaneDrumPad.BlueCymbal     => 2,
                FourLaneDrumPad.GreenDrum or FourLaneDrumPad.GreenCymbal   => 3,
                _                                                          => -1,
            };
        }

        private static int GetFiveLaneFret(int pad)
        {
            return (FiveLaneDrumPad) pad switch
            {
                FiveLaneDrumPad.Red    => 0,
                FiveLaneDrumPad.Yellow => 1,
                FiveLaneDrumPad.Blue   => 2,
                FiveLaneDrumPad.Orange => 3,
                FiveLaneDrumPad.Green  => 4,
                _                      => -1,
            };
        }

        private static int GetSplitFret(int pad)
        {
            return (FourLaneDrumPad) pad switch
            {
                FourLaneDrumPad.RedDrum      => 0,
                FourLaneDrumPad.YellowCymbal => 1,
                FourLaneDrumPad.YellowDrum   => 2,
                FourLaneDrumPad.BlueCymbal   => 3,
                FourLaneDrumPad.BlueDrum     => 4,
                FourLaneDrumPad.GreenCymbal  => 5,
                FourLaneDrumPad.GreenDrum    => 6,
                _                            => -1,
            };
        }
    }
}

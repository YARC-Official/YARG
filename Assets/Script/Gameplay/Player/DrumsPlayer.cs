using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
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

        private Dictionary<int, float> _fretToLastPressedTimeDelta                                         = new();
        private Dictionary<Fret.AnimType, Dictionary<int, float>> _animTypeToFretToLastPressedDelta = new();

        private bool IsSplitMode => Player.Profile.CurrentInstrument is Instrument.ProDrums && Player.Profile.SplitProTomsAndCymbals;

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

            var engine = new YargDrumsEngine(NoteTrack, SyncTrack, EngineParams, Player.Profile.IsBot, Player.Profile.GameMode is GameMode.EliteDrums);
            EngineContainer = GameManager.EngineManager.Register(engine, NoteTrack.Instrument, Chart, Player.RockMeterPreset);

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
            engine.OnStarPowerPhraseMissed += OnStarPowerPhraseMissed;
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
            else if (IsSplitMode)
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
                IsSplitMode,
                ShouldSwapSnareAndHiHat(),
                ShouldSwapCrashAndRide()
            );

            // Particle 0 is always kick fret
            _kickFretFlash.Initialize(colors.GetParticleColor(0).ToUnityColor());

            // Initialize drum activation notes
            NoteTrack.SetDrumActivationFlags(Player.Profile.StarPowerActivationType);
            Notes = NoteTrack.Notes;

            // Set up drum fill lead-ups
            SetDrumFillEffects();

            // Initialize hit timestamps
            InitializeHitTimes();

            // Initialize animation types
            InitializeAnimTypes();

            base.FinishInitialization();
            LaneElement.DefineLaneScale(Player.Profile.CurrentInstrument, _fiveLaneMode ? 5 : 4);
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
                    if (IsSplitMode)
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

                    // Also make sure that the fill effect actually extends to the note
                    if (_trackEffects[candidateIndex].TimeEnd < chord.TimeEnd)
                    {
                        TrackEffect.ExtendEffect(candidateIndex, chord.TimeEnd, NoteSpeed, ref _trackEffects);
                    }
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

        protected override int GetLaneIndex(DrumNote note)
        {
            int laneIndex = note.Pad;

            if (IsSplitMode)
            {
                laneIndex = GetSplitIndex(laneIndex);
            }

            if (!_fiveLaneMode && laneIndex >= (int) FourLaneDrumPad.YellowCymbal && !IsSplitMode)
            {
                laneIndex -= 3;
            }

            if (Player.Profile.LeftyFlip)
            {
                if (_fiveLaneMode)
                {
                    laneIndex = 6 - laneIndex;
                }
                else if (IsSplitMode)
                {
                    laneIndex = 8 - laneIndex;
                }
                else
                {
                    laneIndex = 5 - laneIndex;
                }
            }

            return laneIndex;
        }

        private int GetColorIndex(int index)
        {
            if (IsSplitMode)
            {
                if (Player.Profile.LeftyFlip)
                {
                    index = index switch
                    {
                        0 => 0,
                        7 => 4,
                        6 => 6,
                        5 => 3,
                        4 => 5,
                        3 => 2,
                        2 => 8,
                        1 => 1,
                        _ => index
                    };
                }
                else
                {
                    index = index switch
                    {
                        0 => 0,
                        1 => 1,
                        2 => 5,
                        3 => 2,
                        4 => 6,
                        5 => 3,
                        6 => 7,
                        7 => 4,
                        _ => index
                    };
                }
            }

            if (ShouldSwapSnareAndHiHat())
            {
                if (Player.Profile.LeftyFlip)
                {
                    index = index switch
                    {
                        6 => 4,
                        4 => 6,
                        _ => index
                    };
                }
                else
                {
                    index = index switch
                    {
                        1 => 5,
                        5 => 1,
                        _ => index
                    };
                }
            }

            if (ShouldSwapCrashAndRide())
            {
                if (Player.Profile.LeftyFlip)
                {
                    index = index switch
                    {
                        8 => 5,
                        5 => 8,
                        _ => index
                    };
                }
                else
                {
                    index = index switch
                    {
                        6 => 7,
                        7 => 6,
                        _ => index
                    };
                }
            }

            return index;
        }

        protected override void InitializeSpawnedLane(LaneElement lane, int index)
        {
            Color laneColor;
            int totalLanes;

            if (IsSplitMode)
            {
                totalLanes = 7;
                laneColor = Player.ColorProfile.FourLaneDrums.GetNoteColor(GetColorIndex(index)).ToUnityColor();
                // laneColor = Player.ColorProfile.FourLaneDrums.GetNoteColor(index).ToUnityColor();
            }
            else if (_fiveLaneMode)
            {
                laneColor = Player.ColorProfile.FiveLaneDrums.GetNoteColor(index).ToUnityColor();
                totalLanes = 5;
            }
            else
            {
                laneColor = Player.ColorProfile.FourLaneDrums.GetNoteColor(index).ToUnityColor();
                totalLanes = 4;
            }

            lane.SetAppearance(Player.Profile.CurrentInstrument, index, totalLanes, laneColor);

        }

        protected override void ModifyLaneFromNote(LaneElement lane, DrumNote note)
        {
            if (note.Pad == 0)
            {
                lane.ToggleOpen(true);
            }
            else
            {
                // Correct size of lane slightly for padding in fret array
                lane.MultiplyScale(0.97f);
            }
        }

        protected override void OnNoteHit(int index, DrumNote note)
        {
            base.OnNoteHit(index, note);

            // Remember that drums treat each note separately

            (NotePool.GetByKey(note) as DrumsNoteElement)?.HitNote();

            // The AnimType doesn't actually matter here
            // We handle the animation in OnPadHit instead
            AnimateFret(note.Pad, Fret.AnimType.CorrectNormal);
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

        protected override void OnStarPowerPhraseMissed()
        {
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

        private void OnPadHit(DrumsAction action, bool wasNoteHit, bool wasNoteHitCorrectly, DrumNoteType type, float velocity)
        {
            // Update last hit times for fret flashing animation
            if (action is not DrumsAction.Kick)
            {
                // Play the correct hit animation based on dynamics
                Fret.AnimType animType = Fret.AnimType.CorrectNormal;

                if (DrumNoteType.Accent == type)
                {
                    animType = wasNoteHitCorrectly ? Fret.AnimType.CorrectHard : Fret.AnimType.TooHard;
                }
                else if (DrumNoteType.Ghost == type)
                {
                    animType = wasNoteHitCorrectly ? Fret.AnimType.CorrectSoft : Fret.AnimType.TooSoft;
                }

                ZeroOutHitTime(action, animType);
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
            if (Player.Profile.CurrentInstrument is Instrument.FiveLaneDrums || IsSplitMode)
            {
                return Player.Profile.SwapSnareAndHiHat;
            }

            return false;
        }

        private bool ShouldSwapCrashAndRide() => IsSplitMode && Player.Profile.SwapCrashAndRide;

        protected override void UpdateVisuals(double visualTime)
        {
            base.UpdateVisuals(visualTime);
            UpdateHitTimes();
            UpdateAnimTimes();
            UpdateFretArray();
        }

        private void InitializeHitTimes()
        {
            for (int fret = 0; fret < _fretArray.FretCount; fret++)
            {
                _fretToLastPressedTimeDelta[fret] = float.MaxValue;
            }
        }

        private void InitializeAnimTypes()
        {
            foreach (Fret.AnimType animType in Enum.GetValues(typeof(Fret.AnimType)))
            {
                _animTypeToFretToLastPressedDelta[animType] = new Dictionary<int, float>();

                for (int fret = 0; fret < _fretArray.FretCount; fret++)
                {
                    _animTypeToFretToLastPressedDelta[animType][fret] = float.MaxValue;
                }
            }
        }

        // i.e., flash this fret by making it seem pressed
        private void ZeroOutHitTime(DrumsAction action, Fret.AnimType animType)
        {
            int fret = GetFret(action);
            _fretToLastPressedTimeDelta[fret] = 0f;
            _animTypeToFretToLastPressedDelta[animType][fret] = 0f;
        }

        private void UpdateHitTimes()
        {
            for (int fret = 0; fret < _fretArray.FretCount; fret++)
            {
                _fretToLastPressedTimeDelta[fret] += Time.deltaTime;
            }
        }

        private void UpdateAnimTimes()
        {
            foreach (Fret.AnimType animType in Enum.GetValues(typeof(Fret.AnimType)))
            {
                for (int fret = 0; fret < _fretArray.FretCount; fret++)
                {
                    _animTypeToFretToLastPressedDelta[animType][fret] += Time.deltaTime;
                }
            }
        }

        private void UpdateFretArray()
        {
            for (int fret = 0; fret < _fretArray.FretCount; fret++)
            {
                _fretArray.SetPressedDrum(fret, _fretToLastPressedTimeDelta[fret] < DRUM_PAD_FLASH_HOLD_DURATION, GetAnimType(fret));
                _fretArray.UpdateAccentColorState(fret,
                    _animTypeToFretToLastPressedDelta[Fret.AnimType.CorrectHard][fret] <
                    DRUM_PAD_FLASH_HOLD_DURATION);
            }
        }

        private Fret.AnimType GetAnimType(int fret)
        {
            // Prioritize the length of certain animations
            if (_animTypeToFretToLastPressedDelta[Fret.AnimType.CorrectNormal][fret] < DRUM_PAD_FLASH_HOLD_DURATION)
            {
                return Fret.AnimType.CorrectNormal;
            }

            // Don't hold an accent over a normal note
            if (_animTypeToFretToLastPressedDelta[Fret.AnimType.CorrectHard][fret] < DRUM_PAD_FLASH_HOLD_DURATION)
            {
                return Fret.AnimType.CorrectHard;
            }

            // Don't cut a bright anim short if a ghost is played
            if (_animTypeToFretToLastPressedDelta[Fret.AnimType.CorrectSoft][fret] < DRUM_PAD_FLASH_HOLD_DURATION)
            {
                return Fret.AnimType.CorrectSoft;
            }

            // TODO: Add visuals for wrong amounts of velocity
            return Fret.AnimType.CorrectNormal;
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

        private void AnimateFret(int pad, Fret.AnimType animType)
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

            if (IsSplitMode)
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

            if (IsSplitMode)
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

        private int GetSplitIndex(int pad)
        {
            return (FourLaneDrumPad) pad switch
            {
                FourLaneDrumPad.RedDrum      => ShouldSwapSnareAndHiHat() ? 2 : 1,
                FourLaneDrumPad.YellowCymbal => ShouldSwapSnareAndHiHat() ? 1 : 2,
                FourLaneDrumPad.YellowDrum   => 3,
                FourLaneDrumPad.BlueCymbal   => ShouldSwapCrashAndRide() ? 6 : 4,
                FourLaneDrumPad.BlueDrum     => 5,
                FourLaneDrumPad.GreenCymbal  => ShouldSwapCrashAndRide() ? 4 : 6,
                FourLaneDrumPad.GreenDrum    => 7,
                _                            => -1,
            };
        }
    }
}

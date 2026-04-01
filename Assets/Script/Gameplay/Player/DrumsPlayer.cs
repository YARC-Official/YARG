using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Player;
using YARG.Settings;
using YARG.Themes;
using static YARG.Core.Game.ColorProfile;

namespace YARG.Gameplay.Player
{
    public class DrumsPlayer : TrackPlayer<DrumsEngine, DrumNote>
    {
        private const float DRUM_PAD_FLASH_HOLD_DURATION = 0.2f;

        // Key is a FourLaneDrumPad or FiveLaneDrumPad
        private Dictionary<int, HighwayOrderingInfo> _highwayOrdering;
        private Dictionary<int, int>                 _actionToBreIndex;

        private int DrumsActionToHighwayIndex(DrumsAction action)
        {
            if (_fiveLaneMode)
            {
                return action switch
                {
                    DrumsAction.Kick => (int) FiveLaneDrumPad.Kick,
                    DrumsAction.RedDrum => (int) FiveLaneDrumPad.Red,
                    DrumsAction.YellowCymbal => (int) FiveLaneDrumPad.Yellow,
                    DrumsAction.BlueDrum => (int) FiveLaneDrumPad.Blue,
                    DrumsAction.OrangeCymbal => (int) FiveLaneDrumPad.Orange,
                    DrumsAction.GreenDrum => (int) FiveLaneDrumPad.Green,
                    _ => throw new ArgumentOutOfRangeException(nameof(action))
                };
            }

            return action switch
            {
                DrumsAction.Kick =>         (int) FourLaneDrumPad.Kick,
                DrumsAction.RedDrum =>      (int) FourLaneDrumPad.RedDrum,
                DrumsAction.YellowDrum =>   (int) FourLaneDrumPad.YellowDrum,
                DrumsAction.BlueDrum =>     (int) FourLaneDrumPad.BlueDrum,
                DrumsAction.GreenDrum =>    (int) FourLaneDrumPad.GreenDrum,
                DrumsAction.YellowCymbal => (int) (Player.Profile.SplitProTomsAndCymbals ? FourLaneDrumPad.YellowCymbal : FourLaneDrumPad.YellowDrum),
                DrumsAction.BlueCymbal =>   (int) (Player.Profile.SplitProTomsAndCymbals ? FourLaneDrumPad.BlueCymbal : FourLaneDrumPad.BlueDrum),
                DrumsAction.GreenCymbal =>  (int) (Player.Profile.SplitProTomsAndCymbals ? FourLaneDrumPad.GreenCymbal : FourLaneDrumPad.GreenDrum),
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            };
        }

        public HighwayOrderingInfo GetHighwayOrderingInfo(int pad)
        {
            if (_highwayOrdering.ContainsKey(pad))
            {
                return _highwayOrdering[pad];
            }

            return new(-1, pad);
        }


        public static Dictionary<int, int> DEFAULT_FOUR_LANE_HIGHWAY_ORDERING = new()
        {
            { (int)FourLaneDrumPad.RedDrum,       0 },
            { (int)FourLaneDrumPad.YellowCymbal,  1 },
            { (int)FourLaneDrumPad.YellowDrum,    1 },
            { (int)FourLaneDrumPad.BlueCymbal,    2 },
            { (int)FourLaneDrumPad.BlueDrum,      2 },
            { (int)FourLaneDrumPad.GreenCymbal,   3 },
            { (int)FourLaneDrumPad.GreenDrum,     3 }
        };

        public static Dictionary<int, int> DEFAULT_FIVE_LANE_HIGHWAY_ORDERING = new()
        {
            { (int)FiveLaneDrumPad.Red,       0 },
            { (int)FiveLaneDrumPad.Yellow,    1 },
            { (int)FiveLaneDrumPad.Blue,      2 },
            { (int)FiveLaneDrumPad.Orange,    3 },
            { (int)FiveLaneDrumPad.Green,     4 }
        };

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

        private Dictionary<int, float>                            _fretToLastPressedTimeDelta       = new();
        private Dictionary<Fret.AnimType, Dictionary<int, float>> _animTypeToFretToLastPressedDelta = new();
        private Dictionary<int, int>                              _laneIndexToBreIndex;

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

            if (EngineContainer != null)
            {
                GameManager.EngineManager.Unregister(EngineContainer);
                EngineContainer = null;
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

            engine.OnCodaStart += OnCodaStart;
            engine.OnCodaEnd += OnCodaEnd;

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
            IFretColorProvider colors = !_fiveLaneMode
                ? Player.ColorProfile.FourLaneDrums
                : Player.ColorProfile.FiveLaneDrums;


            var kickFretPrefab = _fiveLaneMode ? ThemeManager.Instance.CreateKickFretPrefabFromTheme(Player.ThemePreset, VisualStyle.FiveLaneDrums) :
                ThemeManager.Instance.CreateKickFretPrefabFromTheme(Player.ThemePreset, VisualStyle.FourLaneDrums);

            MakeHighwayOrdering();

            _fretArray.Initialize(
                _highwayOrdering,
                LaneCount,
                kickFretPrefab,
                colors,
                Player.ThemePreset,
                _fiveLaneMode ? VisualStyle.FiveLaneDrums : VisualStyle.FourLaneDrums
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

            BRELanes = new LaneElement[LaneCount];

            base.FinishInitialization();
            LaneElement.DefineLaneScale(Player.Profile.CurrentInstrument, _fiveLaneMode ? 5 : 4);
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

                var fillLanePosition = GetHighwayOrderingInfo(rightmostNote.Pad).Position;

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
                    _trackEffects[candidateIndex].FillLanePosition = fillLanePosition;
                    _trackEffects[candidateIndex].TotalLanes = LaneCount;
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

        protected override void InitializeSpawnedLane(LaneElement lane, DrumNote note)
        {
            var highwayOrderingInfo = _highwayOrdering[note.Pad];

            var laneColor = (_fiveLaneMode ?
                Player.ColorProfile.FiveLaneDrums.GetNoteColor(highwayOrderingInfo.ColorIndex) :
                Player.ColorProfile.FourLaneDrums.GetNoteColor(highwayOrderingInfo.ColorIndex)
            ).ToUnityColor();

            lane.SetAppearance(
                Player.Profile.CurrentInstrument,
                note.LaneNote,
                highwayOrderingInfo.Position,
                LaneCount,
                laneColor
            );

        }

        protected override void InitializeSpawnedLane(LaneElement lane, int laneIndex)
        {
            int highwayIndex = -1;
            HighwayOrderingInfo highwayOrderingInfo = default;
            foreach ((int index, var info) in _highwayOrdering)
            {
                if (laneIndex == Mathf.RoundToInt(info.Position))
                {
                    highwayIndex = index;
                    highwayOrderingInfo = info;
                    break;
                }
            }

            if (highwayIndex == -1)
            {
                YargLogger.LogError("Unable to find highway index for lane index " + laneIndex + " in drums player.");
                return;
            }

            var laneColor = (_fiveLaneMode ?
                    Player.ColorProfile.FiveLaneDrums.GetNoteColor(highwayOrderingInfo.ColorIndex) :
                    Player.ColorProfile.FourLaneDrums.GetNoteColor(highwayOrderingInfo.ColorIndex)
                ).ToUnityColor();

            lane.SetAppearance(
                Player.Profile.CurrentInstrument,
                highwayIndex,
                highwayOrderingInfo.Position,
                LaneCount,
                laneColor
                );
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

        protected override void RescaleLanesForBRE()
        {
            int subdivisions = 4;

            if (_fiveLaneMode)
            {
                subdivisions = 5;
            }
            else if (IsSplitMode)
            {
                subdivisions = 7;
            }

            LaneElement.DefineLaneScale(Player.Profile.CurrentInstrument, subdivisions, true);
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

        private void OnLaneHit(int fret)
        {
            fret = DrumsActionToHighwayIndex((DrumsAction) fret);
            _fretArray.PlayCodaHitAnimation(fret);
        }

        protected override void OnCodaStart(CodaSection coda)
        {
            base.OnCodaStart(coda);
            CurrentCoda.OnLaneHit += OnLaneHit;

            _fretArray.SetBreMode(true);
        }

        protected override void OnCodaEnd(CodaSection coda)
        {
            base.OnCodaEnd(coda);
            CurrentCoda.OnLaneHit -= OnLaneHit;

            _fretArray.SetBreMode(false);
        }


        private void OnPadHit(DrumsAction action, bool wasNoteHit, bool wasNoteHitCorrectly, DrumNoteType type, float velocity)
        {
            var fret = DrumsActionToHighwayIndex(action);

            // This is done here for drums rather than in-engine because engine doesn't know about pad ordering
            if (Engine.IsCodaActive)
            {
                CurrentCoda.HitLane(GameManager.VisualTime, (int) action);
            }

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
            return Engine.NoteIndex == 0 ||        // Can freestyle before first note is hit/missed
                Engine.NoteIndex >= Notes.Count || // Can freestyle after last note
                Engine.IsWaitCountdownActive ||    // Can freestyle during WaitCountdown
                Engine.IsCodaActive;               // Can freestyle during Coda
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

            if (Engine.IsCodaActive)
            {
                // Set emission color of BRE lanes depending on time since last hit

                foreach (var (k, v) in _laneIndexToBreIndex)
                {
                    BRELanes[k].SetEmissionColor(CurrentCoda.GetNormalizedTimeSinceLastHit(v, visualTime));
                }
            }

            UpdateHitTimes();
            UpdateAnimTimes();
            UpdateFretArray();
        }

        private void InitializeHitTimes()
        {
            foreach (var fretIdx in _highwayOrdering.Keys)
            {
                _fretToLastPressedTimeDelta[fretIdx] = float.MaxValue;
            }
        }

        private void InitializeAnimTypes()
        {
            foreach (Fret.AnimType animType in Enum.GetValues(typeof(Fret.AnimType)))
            {
                _animTypeToFretToLastPressedDelta[animType] = new Dictionary<int, float>();

                foreach (var fretIdx in _highwayOrdering.Keys)
                {
                    _animTypeToFretToLastPressedDelta[animType][fretIdx] = float.MaxValue;
                }
            }
        }

        // i.e., flash this fret by making it seem pressed
        private void ZeroOutHitTime(DrumsAction action, Fret.AnimType animType)
        {
            int fretIdx = DrumsActionToHighwayIndex(action);
            _fretToLastPressedTimeDelta[fretIdx] = 0f;
            _animTypeToFretToLastPressedDelta[animType][fretIdx] = 0f;
        }

        private void UpdateHitTimes()
        {
            foreach (var fretIdx in _highwayOrdering.Keys)
            {
                _fretToLastPressedTimeDelta[fretIdx] += Time.deltaTime;
            }
        }

        private void UpdateAnimTimes()
        {
            foreach (Fret.AnimType animType in Enum.GetValues(typeof(Fret.AnimType)))
            {
                foreach (var fretIdx in _highwayOrdering.Keys)
                {
                    _animTypeToFretToLastPressedDelta[animType][fretIdx] += Time.deltaTime;
                }
            }
        }

        private void UpdateFretArray()
        {
            foreach (var fretIdx in _highwayOrdering.Keys)
            {
                _fretArray.SetPressedDrum(fretIdx, _fretToLastPressedTimeDelta[fretIdx] < DRUM_PAD_FLASH_HOLD_DURATION, GetAnimType(fretIdx));
                _fretArray.UpdateAccentColorState(fretIdx,
                    _animTypeToFretToLastPressedDelta[Fret.AnimType.CorrectHard][fretIdx] <
                    DRUM_PAD_FLASH_HOLD_DURATION);
            }
        }

        private Fret.AnimType GetAnimType(int fretIdx)
        {
            // Prioritize the length of certain animations
            if (_animTypeToFretToLastPressedDelta[Fret.AnimType.CorrectNormal][fretIdx] < DRUM_PAD_FLASH_HOLD_DURATION)
            {
                return Fret.AnimType.CorrectNormal;
            }

            // Don't hold an accent over a normal note
            if (_animTypeToFretToLastPressedDelta[Fret.AnimType.CorrectHard][fretIdx] < DRUM_PAD_FLASH_HOLD_DURATION)
            {
                return Fret.AnimType.CorrectHard;
            }

            // Don't cut a bright anim short if a ghost is played
            if (_animTypeToFretToLastPressedDelta[Fret.AnimType.CorrectSoft][fretIdx] < DRUM_PAD_FLASH_HOLD_DURATION)
            {
                return Fret.AnimType.CorrectSoft;
            }

            // TODO: Add visuals for wrong amounts of velocity
            return Fret.AnimType.CorrectNormal;
        }

        private void AnimateAction(DrumsAction action)
        {
            var index = DrumsActionToHighwayIndex(action);

            if (_fiveLaneMode)
            {
                // Only use cymbal animation if the cymbal gems are being used
                if (Player.Profile.UseCymbalModels && action is DrumsAction.YellowCymbal or DrumsAction.OrangeCymbal)
                {
                    _fretArray.PlayCymbalHitAnimation(index);
                }
                else
                {
                    _fretArray.PlayHitAnimation(index);
                }

                return;
            }

            // Can technically merge this condition with the above, but it's more readable like this
            if (action is DrumsAction.YellowCymbal or DrumsAction.BlueCymbal or DrumsAction.GreenCymbal)
            {
                _fretArray.PlayCymbalHitAnimation(index);
            }
            else
            {
                _fretArray.PlayHitAnimation(index);
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

            if (_fiveLaneMode)
            {
                // Only use cymbal animation if the cymbal gems are being used
                if (Player.Profile.UseCymbalModels && (FiveLaneDrumPad) pad
                    is FiveLaneDrumPad.Yellow
                    or FiveLaneDrumPad.Orange)
                {
                    _fretArray.PlayCymbalHitAnimation(pad);
                }
                else
                {
                    _fretArray.PlayHitAnimation(pad);
                }

                return;
            }

            // Can technically merge this condition with the above, but it's more readable like this
            if ((FourLaneDrumPad) pad
                is FourLaneDrumPad.YellowCymbal
                or FourLaneDrumPad.BlueCymbal
                or FourLaneDrumPad.GreenCymbal)
            {
                _fretArray.PlayCymbalHitAnimation(pad);
            }
            else
            {
                _fretArray.PlayHitAnimation(pad);
            }
        }

        private int ApplyHandednessToPosition(int position)
        {
            if (Player.Profile.LeftyFlip)
            {
                return LaneCount - position - 1;
            }

            return position;
        }

        private int ApplyHandednessToFourLaneColor(FourLaneDrumsFret fret)
        {
            if (Player.Profile.LeftyFlip)
            {
                return fret switch
                {
                    FourLaneDrumsFret.RedDrum =>        (int)FourLaneDrumsFret.GreenDrum,
                    FourLaneDrumsFret.YellowDrum =>     (int)FourLaneDrumsFret.BlueDrum,
                    FourLaneDrumsFret.BlueDrum =>       (int)FourLaneDrumsFret.YellowDrum,
                    FourLaneDrumsFret.GreenDrum =>      (int)FourLaneDrumsFret.RedDrum,
                    FourLaneDrumsFret.YellowCymbal =>   (int)FourLaneDrumsFret.BlueCymbal,
                    FourLaneDrumsFret.BlueCymbal =>     (int)FourLaneDrumsFret.YellowCymbal,
                    FourLaneDrumsFret.GreenCymbal =>    (int)FourLaneDrumsFret.RedCymbal,
                    _ => (int) fret
                };
            }

            return (int) fret;
        }

        private int ApplyHandednessToFiveLaneColor(FiveLaneDrumsFret fret)
        {
            if (Player.Profile.LeftyFlip)
            {
                return fret switch {
                    FiveLaneDrumsFret.Red =>    (int)FiveLaneDrumsFret.Green,
                    FiveLaneDrumsFret.Yellow => (int)FiveLaneDrumsFret.Orange,
                    FiveLaneDrumsFret.Blue =>   (int)FiveLaneDrumsFret.Blue,
                    FiveLaneDrumsFret.Orange => (int)FiveLaneDrumsFret.Yellow,
                    FiveLaneDrumsFret.Green =>  (int)FiveLaneDrumsFret.Red,
                    _ => (int)fret
                };
            }

            return (int)fret;
        }

        private void MakeHighwayOrdering()
        {
            if (Player.Profile.CurrentInstrument is Instrument.FiveLaneDrums)
            {
                LaneCount = 5;
                _highwayOrdering = new()
                {
                    { (int)FiveLaneDrumPad.Red,    new(ApplyHandednessToPosition(Player.Profile.SwapSnareAndHiHat ? 1 : 0), ApplyHandednessToFiveLaneColor(FiveLaneDrumsFret.Red) ) },
                    { (int)FiveLaneDrumPad.Yellow, new(ApplyHandednessToPosition(Player.Profile.SwapSnareAndHiHat ? 0 : 1), ApplyHandednessToFiveLaneColor(FiveLaneDrumsFret.Yellow) ) },
                    { (int)FiveLaneDrumPad.Blue,   new(ApplyHandednessToPosition(2),                                        (int)FiveLaneDrumsFret.Blue) }, // No need to waste a function call on this
                    { (int)FiveLaneDrumPad.Orange, new(ApplyHandednessToPosition(3),                                        ApplyHandednessToFiveLaneColor(FiveLaneDrumsFret.Orange) ) },
                    { (int)FiveLaneDrumPad.Green,  new(ApplyHandednessToPosition(4),                                        ApplyHandednessToFiveLaneColor(FiveLaneDrumsFret.Green) ) }
                };

                _actionToBreIndex = new Dictionary<int, int>
                {
                    { 0, 0 },
                    { 1, 1 },
                    { 2, 2 },
                    { 3, 3 },
                    { 4, 4 }
                };

                _laneIndexToBreIndex = new Dictionary<int, int>
                {
                    { ApplyHandednessToPosition(0), 0 },
                    { ApplyHandednessToPosition(1), 1 },
                    { ApplyHandednessToPosition(2), 2 },
                    { ApplyHandednessToPosition(3), 3 },
                    { ApplyHandednessToPosition(4), 4 }
                };
            }
            else if (Player.Profile.SplitProTomsAndCymbals && Player.Profile.CurrentInstrument is Instrument.ProDrums)
            {
                LaneCount = 7;
                _highwayOrdering = new()
                {
                    { (int)FourLaneDrumPad.RedDrum,       new(ApplyHandednessToPosition(Player.Profile.SwapSnareAndHiHat ? 1 : 0),   ApplyHandednessToFourLaneColor(FourLaneDrumsFret.RedDrum)) },
                    { (int)FourLaneDrumPad.YellowCymbal,  new(ApplyHandednessToPosition(Player.Profile.SwapSnareAndHiHat ? 0 : 1),   ApplyHandednessToFourLaneColor(FourLaneDrumsFret.YellowCymbal)) },
                    { (int)FourLaneDrumPad.YellowDrum,    new(ApplyHandednessToPosition(2),                                          ApplyHandednessToFourLaneColor(FourLaneDrumsFret.YellowDrum)) },
                    { (int)FourLaneDrumPad.BlueCymbal,    new(ApplyHandednessToPosition(Player.Profile.SwapCrashAndRide ? 5 : 3),    ApplyHandednessToFourLaneColor(FourLaneDrumsFret.BlueCymbal)) },
                    { (int)FourLaneDrumPad.BlueDrum,      new(ApplyHandednessToPosition(4),                                          ApplyHandednessToFourLaneColor(FourLaneDrumsFret.BlueDrum)) },
                    { (int)FourLaneDrumPad.GreenCymbal,   new(ApplyHandednessToPosition(Player.Profile.SwapCrashAndRide ? 3 : 5),    ApplyHandednessToFourLaneColor(FourLaneDrumsFret.GreenCymbal)) },
                    { (int)FourLaneDrumPad.GreenDrum,     new(ApplyHandednessToPosition(6),                                          ApplyHandednessToFourLaneColor(FourLaneDrumsFret.GreenDrum)) },
                };

                _actionToBreIndex = new Dictionary<int, int>
                {
                    { 0,  0 }, // RedDrum
                    { 1,  1 }, // YellowDrum
                    { 2,  2 }, // BlueDrum
                    { 3,  3 }, // GreenDrum
                    { 4,  1 }, // YellowCymbal
                    { 5,  2 }, // BlueCymbal
                    { 6,  3 }  // GreenCymbal
                };

                _laneIndexToBreIndex = new Dictionary<int, int> {
                    { ApplyHandednessToPosition(Player.Profile.SwapSnareAndHiHat ? 1 : 0), 0 }, // RedDrum
                    { ApplyHandednessToPosition(2), 1 }, // YellowDrum
                    { ApplyHandednessToPosition(4), 2 }, // BlueDrum
                    { ApplyHandednessToPosition(6), 3 }, // GreenDrum
                    { ApplyHandednessToPosition(Player.Profile.SwapSnareAndHiHat ? 0 : 1), 4 }, // YellowCymbal
                    { ApplyHandednessToPosition(Player.Profile.SwapCrashAndRide ? 5 : 3), 5 }, // BlueCymbal
                    { ApplyHandednessToPosition(Player.Profile.SwapCrashAndRide ? 3 : 5), 6 }  // GreenCymbal
                };
            }
            else
            {
                LaneCount = 4;
                _highwayOrdering = new()
                {
                    { (int)FourLaneDrumPad.RedDrum,       new(ApplyHandednessToPosition(0), ApplyHandednessToFourLaneColor(FourLaneDrumsFret.RedDrum)) },
                    { (int)FourLaneDrumPad.YellowCymbal,  new(ApplyHandednessToPosition(1), ApplyHandednessToFourLaneColor(FourLaneDrumsFret.YellowCymbal)) },
                    { (int)FourLaneDrumPad.YellowDrum,    new(ApplyHandednessToPosition(1), ApplyHandednessToFourLaneColor(FourLaneDrumsFret.YellowDrum)) },
                    { (int)FourLaneDrumPad.BlueCymbal,    new(ApplyHandednessToPosition(2), ApplyHandednessToFourLaneColor(FourLaneDrumsFret.BlueCymbal)) },
                    { (int)FourLaneDrumPad.BlueDrum,      new(ApplyHandednessToPosition(2), ApplyHandednessToFourLaneColor(FourLaneDrumsFret.BlueDrum)) },
                    { (int)FourLaneDrumPad.GreenCymbal,   new(ApplyHandednessToPosition(3), ApplyHandednessToFourLaneColor(FourLaneDrumsFret.GreenCymbal)) },
                    { (int)FourLaneDrumPad.GreenDrum,     new(ApplyHandednessToPosition(3), ApplyHandednessToFourLaneColor(FourLaneDrumsFret.GreenDrum)) },
                };

                _actionToBreIndex = new Dictionary<int, int>
                {
                    { 0, 0 }, // RedDrum
                    { 1, 1 }, // YellowDrum
                    { 2, 2 }, // BlueDrum
                    { 3, 3 }, // GreenDrum
                    { 4, 1 }, // YellowCymbal
                    { 5, 2 }, // BlueCymbal
                    { 6, 3 }, // GreenCymbal
                };

                _laneIndexToBreIndex = new Dictionary<int, int> {
                    { ApplyHandednessToPosition(0), 0 }, // RedDrum
                    { ApplyHandednessToPosition(1), 1 }, // YellowDrum and YellowCymbal
                    { ApplyHandednessToPosition(2), 2 }, // BlueDrum and BlueCymbal
                    { ApplyHandednessToPosition(3), 3 }, // GreenDrum and GreenCymbal
                };
            }
        }

        protected override Dictionary<int, int> GetLaneIndexes() => _actionToBreIndex;
    }
}

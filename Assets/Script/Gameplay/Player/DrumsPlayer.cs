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
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.HighwayConfiguration;
using YARG.Player;
using YARG.Settings;
using YARG.Themes;
using static YARG.Core.Game.ColorProfile;

namespace YARG.Gameplay.Player
{
    public class DrumsPlayer : TrackPlayer<DrumsEngine, DrumNote>
    {
        private const float DRUM_PAD_FLASH_HOLD_DURATION = 0.2f;
        private static readonly Fret.AnimType[] AnimTypes = (Fret.AnimType[]) Enum.GetValues(typeof(Fret.AnimType));

        // Key is a FourLaneDrumPad or FiveLaneDrumPad
        private Dictionary<int, HighwayOrderingInfo> _highwayOrdering = new();

        // The highway ordering is indexed by pad, so we need a special value for dedicated-lane double kicks, since those are
        // indistinguishable from 1x kicks by pad number
        public const int DOUBLE_KICK_FRET_INDEX = int.MaxValue;

        private bool _yellowCymbalHasLane = false;
        private bool _blueCymbalHasLane = false;
        private bool _greenCymbalHasLane = false;

        public int NumberOfDedicatedKickLanes { get; private set; } = 0;


        public float NoteScaleFactor = 1f;
        private float _baselineLaneCount => _fiveLaneMode ? 5f : 4f;

        // When an action happens, we'll use this to determine which _actionToMostRecentTime entry to update
        // This is often 1:1, but non-split 4L maps multiple actions to the shared lanes
        private Dictionary<DrumsAction, DrumsBreLaneIndex> _actionToBreLaneIndex = new();

        // When a BRE lane element needs to know how bright it should be, it'll use this table to get the right BRE lane index
        private Dictionary<int, DrumsBreLaneIndex> _highwayOrderingIndexToBreLaneIndex = new();

        // Record of the most recent time that each BRE lane has been lit up by any of the actions that map to it
        private Dictionary<DrumsBreLaneIndex, double> _breLaneIndexToMostRecentTime = new();

        private int DrumsActionToPad(DrumsAction action)
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
                    DrumsAction.WildcardPad => (int) FiveLaneDrumPad.Kick,
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
                    DrumsAction.YellowCymbal => (int) (_yellowCymbalHasLane ? FourLaneDrumPad.YellowCymbal : FourLaneDrumPad.YellowDrum),
                    DrumsAction.BlueCymbal =>   (int) (_blueCymbalHasLane ? FourLaneDrumPad.BlueCymbal : FourLaneDrumPad.BlueDrum),
                    DrumsAction.GreenCymbal =>  (int) (_greenCymbalHasLane ? FourLaneDrumPad.GreenCymbal : FourLaneDrumPad.GreenDrum),
                    DrumsAction.WildcardPad =>  (int)FourLaneDrumPad.Kick,
                    _ => throw new ArgumentOutOfRangeException(nameof(action))
                };
        }

        public HighwayOrderingInfo GetHighwayOrderingInfo(int pad)
        {
            if (_highwayOrdering.TryGetValue(pad, out var info))
            {
                return info;
            }

            return new HighwayOrderingInfo(-1, pad);
        }

        public static Dictionary<int, int> DEFAULT_FOUR_LANE_HIGHWAY_ORDERING = new()
        {
            { (int)FourLaneDrumPad.RedDrum,       0 },
            { (int)FourLaneDrumPad.YellowDrum,    1 },
            { (int)FourLaneDrumPad.YellowCymbal,  1 },
            { (int)FourLaneDrumPad.BlueDrum,      2 },
            { (int)FourLaneDrumPad.BlueCymbal,    2 },
            { (int)FourLaneDrumPad.GreenDrum,     3 },
            { (int)FourLaneDrumPad.GreenCymbal,   3 }
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

        protected override float[] StarMultiplierThresholds { get; set; } =
        {
            0.06f, 0.12f, 0.2f, 0.45f, 0.75f, 1.09f
        };

        private int[] _drumSoundEffectRoundRobin = new int[8];
        private float _drumSoundEffectAccentThreshold;

        private Dictionary<int, float>                            _padToLastPressedTimeDelta       = new();
        private Dictionary<Fret.AnimType, Dictionary<int, float>> _animTypeToPadToLastPressedDelta = new();


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
                EngineParams = Player.EnginePreset.Drums.Create(StarMultiplierThresholds, SoloBonusStarMultiplierThresholds, mode);
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
            engine.OnStarPowerReady += OnStarPowerReady;

            engine.OnCountdownChange += OnCountdownChange;

            engine.OnPadHit += OnPadHit;

            return engine;
        }

        protected override void FinishInitialization()
        {
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

        public override void ResetPracticeSection()
        {
            base.ResetPracticeSection();
            _fretArray.ResetAll();
        }

        protected override void ResetLastHitTimes()
        {
            foreach (var breLaneIndex in _highwayOrderingIndexToBreLaneIndex.Values)
            {
                _breLaneIndexToMostRecentTime[breLaneIndex] = 0;
            }
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

                var highwayOrderingIndex = rightmostNote.IsDoubleKick ? DOUBLE_KICK_FRET_INDEX : rightmostNote.Pad;

                var fillLanePosition = GetHighwayOrderingInfo(highwayOrderingIndex).Position;

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
                if (laneIndex == info.Position)
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
            LaneElement.DefineLaneScale(Player.Profile.CurrentInstrument, LaneCount, true);
        }

        protected override void OnNoteHit(int index, DrumNote note)
        {
            base.OnNoteHit(index, note);

            // Remember that drums treat each note separately

            (NotePool.GetByKey(note) as DrumsNoteElement)?.HitNote();

            // The AnimType doesn't actually matter here
            // We handle the animation in OnPadHit instead

            int animationIndex;
            if (note.IsDoubleKick && NumberOfDedicatedKickLanes is 2)
            {
                animationIndex = DOUBLE_KICK_FRET_INDEX;
            }
            else
            {
                animationIndex = note.Pad;
            }

            AnimateFret(animationIndex, Fret.AnimType.CorrectNormal);
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
            fret = DrumsActionToPad((DrumsAction) fret);
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


        private void OnPadHit(DrumsAction action, bool wasNoteHit, bool wasNoteHitCorrectly, bool wasOverhitInLane, DrumNoteType type, float velocity)
        {
            var fret = DrumsActionToPad(action);

            // This is done here for drums rather than in-engine because engine doesn't know about pad ordering
            if (Engine.IsCodaActive)
            {
                CurrentCoda.HitLane(GameManager.VisualTime, (int) action);

                if (_actionToBreLaneIndex.TryGetValue(action, out var breLaneIndex))
                {
                    _breLaneIndexToMostRecentTime[breLaneIndex] = GameManager.VisualTime;
                }

            }

            var kickHasLane = NumberOfDedicatedKickLanes > 0;

            // Update last hit times for fret flashing animation
            if ((action is not DrumsAction.Kick || kickHasLane) && !wasOverhitInLane)
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
            // Also skip if it was an overhit in a lane, since we only want it to play the AODSFX
            if (wasNoteHit || wasOverhitInLane)
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

            if ((action is not DrumsAction.Kick || kickHasLane) && action is not DrumsAction.WildcardPad)
            {
                if (isDrumFreestyle)
                {
                    AnimateAction(action);

                    // Special case for split-dedicated kicks; we don't know which pedal was used, so animate both
                    if (action is DrumsAction.Kick && NumberOfDedicatedKickLanes is 2)
                    {
                        _fretArray.PlayHitAnimation(DOUBLE_KICK_FRET_INDEX);
                    }
                }
                else
                {
                    _fretArray.PlayMissAnimation(fret);

                    // Special case for split-dedicated kicks; we don't know which pedal was used, so animate both
                    if (action is DrumsAction.Kick && NumberOfDedicatedKickLanes is 2)
                    {
                        _fretArray.PlayMissAnimation(DOUBLE_KICK_FRET_INDEX);
                    }
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

            if (actionIndex == (int) DrumsAction.WildcardPad)
            {
                return;
            }

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
            return (frame, Engine.EngineStats.ConstructReplayStats(Player.Profile.Name, Player.IsReplay));
        }

        protected override void UpdateVisuals(double visualTime)
        {
            base.UpdateVisuals(visualTime);

            if (Engine.IsCodaActive)
            {
                // Set emission color of BRE lanes depending on time since last hit

                foreach (var (highwayOrderingIndex, breLaneIndex) in _highwayOrderingIndexToBreLaneIndex)
                {
                    var mostRecentTime = _breLaneIndexToMostRecentTime[breLaneIndex];
                    var normalizedTimeSinceLastHit = CodaSection.GetNormalizedTimeSinceLastHit(visualTime, mostRecentTime);
                    BRELanes[highwayOrderingIndex].SetEmissionColor(normalizedTimeSinceLastHit);
                }
            }

            UpdateHitTimes();
            UpdateAnimTimes();
            UpdateFretArray();
        }

        private void InitializeHitTimes()
        {
            foreach (var pad in _highwayOrdering.Keys)
            {
                _padToLastPressedTimeDelta[pad] = float.MaxValue;
            }
        }

        private void InitializeAnimTypes()
        {
            foreach (var animType in AnimTypes)
            {
                _animTypeToPadToLastPressedDelta[animType] = new Dictionary<int, float>();

                foreach (var pad in _highwayOrdering.Keys)
                {
                    _animTypeToPadToLastPressedDelta[animType][pad] = float.MaxValue;
                }
            }
        }

        // i.e., flash this fret by making it seem pressed
        private void ZeroOutHitTime(DrumsAction action, Fret.AnimType animType)
        {
            int pad = DrumsActionToPad(action);
            ZeroOutHitTime(pad, animType);

            // When kicks have split dedicated lanes, zero out both for kick inputs
            if (action is DrumsAction.Kick && NumberOfDedicatedKickLanes == 2)
            {
                ZeroOutHitTime(DOUBLE_KICK_FRET_INDEX, animType);
            }
        }

        // i.e., flash this fret by making it seem pressed
        private void ZeroOutHitTime(int pad, Fret.AnimType animType)
        {
            _padToLastPressedTimeDelta[pad] = 0f;
            _animTypeToPadToLastPressedDelta[animType][pad] = 0f;
        }

        private void UpdateHitTimes()
        {
            foreach (var pad in _highwayOrdering.Keys)
            {
                _padToLastPressedTimeDelta[pad] += Time.deltaTime;
            }
        }

        private void UpdateAnimTimes()
        {
            foreach (var animType in AnimTypes)
            {
                foreach (var pad in _highwayOrdering.Keys)
                {
                    _animTypeToPadToLastPressedDelta[animType][pad] += Time.deltaTime;
                }
            }
        }

        private void UpdateFretArray()
        {
            // For each visual fret...
            for (var i = 0; i < LaneCount; i++)
            {
                // ...find the lowest last-pressed delta among pads that the visual fret represents
                // Usually there's just one pad for each fret, but tom+cymbal frets have two so we
                // want to take the minimum of those

                var lowestDelta = float.MaxValue;

                // The fret array's animation functions take pads, so we need to know which pad is relevant for the
                // current visual fret. If there are multiple (shared fret), we can send it either; the fret array
                // forwards shared fret indices to the right fret. If we don't find any pads, we won't call any
                // animations.
                int? padToPress = null;

                foreach (var (pad, highwayOrderingInfo) in _highwayOrdering)
                {
                    // We only care about pads that are mapped to this position
                    if (highwayOrderingInfo.Position == i)
                    {
                        padToPress ??= pad;
                        if (_padToLastPressedTimeDelta[pad] < lowestDelta)
                        {
                            lowestDelta = _padToLastPressedTimeDelta[pad];
                        }
                    }
                }

                if (padToPress is not null)
                {
                    _fretArray.SetPressedDrum(padToPress.Value, lowestDelta < DRUM_PAD_FLASH_HOLD_DURATION, GetAnimType(padToPress.Value));
                    _fretArray.UpdateAccentColorState(padToPress.Value,
                        _animTypeToPadToLastPressedDelta[Fret.AnimType.CorrectHard][padToPress.Value] <
                        DRUM_PAD_FLASH_HOLD_DURATION);
                }
            }
        }

        private Fret.AnimType GetAnimType(int fretIdx)
        {
            // Prioritize the length of certain animations
            if (_animTypeToPadToLastPressedDelta[Fret.AnimType.CorrectNormal][fretIdx] < DRUM_PAD_FLASH_HOLD_DURATION)
            {
                return Fret.AnimType.CorrectNormal;
            }

            // Don't hold an accent over a normal note
            if (_animTypeToPadToLastPressedDelta[Fret.AnimType.CorrectHard][fretIdx] < DRUM_PAD_FLASH_HOLD_DURATION)
            {
                return Fret.AnimType.CorrectHard;
            }

            // Don't cut a bright anim short if a ghost is played
            if (_animTypeToPadToLastPressedDelta[Fret.AnimType.CorrectSoft][fretIdx] < DRUM_PAD_FLASH_HOLD_DURATION)
            {
                return Fret.AnimType.CorrectSoft;
            }

            // TODO: Add visuals for wrong amounts of velocity
            return Fret.AnimType.CorrectNormal;
        }

        private void AnimateAction(DrumsAction action)
        {
            var index = DrumsActionToPad(action);

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
            if ((pad == (int) FourLaneDrumPad.Kick && NumberOfDedicatedKickLanes == 0) || pad == (int)FourLaneDrumPad.Wildcard)
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

        private void MakeHighwayOrdering()
        {
            var instrument = Player.Profile.CurrentInstrument;

            var ordering = instrument switch
            {
                Instrument.FourLaneDrums => Player.Profile.FourLaneDrumsHighwayOrdering,
                Instrument.ProDrums => Player.Profile.ProDrumsHighwayOrdering,
                Instrument.FiveLaneDrums => Player.Profile.FiveLaneDrumsHighwayOrdering,
                _ => throw new ArgumentOutOfRangeException("Unexpected nondrums instrument")
            };

            // If the player has a dedicated Double Kick lane that's set to Expert+ Only, and isn't playing on Expert+, then the actual amount of lanes is 1 fewer than the size
            // of the provided ordering because that lane is absent.
            LaneCount = ordering.Length - (ordering.Contains(DrumsHighwayItem.Kick2xConditional) && Player.Profile.CurrentDifficulty is not Difficulty.ExpertPlus ? 1 : 0);
            NoteScaleFactor = _baselineLaneCount / LaneCount;

            // Once we've skipped the conditional Double Kick lane (when not present), we'll have an off-by-one relationship between i and the actual intended position
            var skippedPedalAdjustment = 0;
            for (var i = 0; i < ordering.Length; i++)
            {
                var item = ordering[i];

                if (item is DrumsHighwayItem.Kick2xConditional && Player.Profile.CurrentDifficulty is not Difficulty.ExpertPlus)
                {
                    skippedPedalAdjustment = 1;
                    continue;
                }

                var adjustedIndex = i - skippedPedalAdjustment;

                var highwayOrderingInfo = DrumsHighwayItemView.GetHighwayOrderingInfo(item, instrument);

                _highwayOrderingIndexToBreLaneIndex.Add(ApplyHandednessToPosition(adjustedIndex), highwayOrderingInfo.BreLaneIndex);

                switch (item)
                {
                    case DrumsHighwayItem.Kick or DrumsHighwayItem.Kick1x or DrumsHighwayItem.Kick2x or DrumsHighwayItem.Kick2xConditional:
                        NumberOfDedicatedKickLanes++;
                        break;
                    case DrumsHighwayItem.YellowCymbal:
                        _yellowCymbalHasLane = true;
                        break;
                    case DrumsHighwayItem.BlueCymbal:
                        _blueCymbalHasLane = true;
                        break;
                    case DrumsHighwayItem.GreenCymbal:
                        _greenCymbalHasLane = true;
                        break;
                }

                foreach (var highwayOrderingElement in highwayOrderingInfo.Elements)
                {
                    _highwayOrdering[highwayOrderingElement.Pad] = new(
                        ApplyHandednessToPosition(adjustedIndex),
                        DrumsColorHelpers.ApplyHandednessToColor(highwayOrderingElement.ColorIndex, Player.Profile.LeftyFlip, NumberOfDedicatedKickLanes == 2, instrument)
                    );

                    if (!_actionToBreLaneIndex.ContainsKey(highwayOrderingElement.Action)) {
                        _actionToBreLaneIndex.Add(highwayOrderingElement.Action, highwayOrderingInfo.BreLaneIndex);
                    }
                }
            }

            ResetLastHitTimes();
        }

        public enum DrumsBreLaneIndex
        {
            Kick,

            Red,

            Yellow,
            YellowDrum,
            YellowCymbal,

            Blue,
            BlueDrum,
            BlueCymbal,

            Green,
            GreenDrum,
            GreenCymbal,

            Orange
        }
    }
}

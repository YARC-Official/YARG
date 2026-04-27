using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Playback;
using YARG.Player;
using YARG.Settings;
using YARG.Themes;
using static YARG.Core.Game.ColorProfile;
using Random = UnityEngine.Random;

namespace YARG.Gameplay.Player
{
    public class SixFretGuitarPlayer : TrackPlayer<GuitarEngine, GuitarNote>
    {
        private const double SUSTAIN_END_MUTE_THRESHOLD = 0.1;

        // Combined lane pair index: (Black1,White1)->0, (Black2,White2)->1, (Black3,White3)->2
        private static readonly Dictionary<SixFretGuitarFret, int> COMBINED_PAIR_INDEX = new()
        {
            { SixFretGuitarFret.Black1, 0 }, { SixFretGuitarFret.White1, 0 },
            { SixFretGuitarFret.Black2, 1 }, { SixFretGuitarFret.White2, 1 },
            { SixFretGuitarFret.Black3, 2 }, { SixFretGuitarFret.White3, 2 },
        };

        private static int GetPairIndex(SixFretGuitarFret fret) => COMBINED_PAIR_INDEX[fret];

        public new virtual int LaneCount => 6;

        protected virtual Dictionary<int, int> GetDefaultHighwayOrdering()
        {
            return new()
            {
                { (int)SixFretGuitarFret.Black1,     0 },
                { (int)SixFretGuitarFret.White1,     1 },
                { (int)SixFretGuitarFret.Black2,     2 },
                { (int)SixFretGuitarFret.White2,     3 },
                { (int)SixFretGuitarFret.Black3,     4 },
                { (int)SixFretGuitarFret.White3,     5 }
            };
        }

        public static Dictionary<int, int> DEFAULT_HIGHWAY_ORDERING { get; } = new()
        {
            { (int)SixFretGuitarFret.Black1,     0 },
            { (int)SixFretGuitarFret.White1,     1 },
            { (int)SixFretGuitarFret.Black2,     2 },
            { (int)SixFretGuitarFret.White2,     3 },
            { (int)SixFretGuitarFret.Black3,     4 },
            { (int)SixFretGuitarFret.White3,     5 }
        };

        protected virtual SixFretGuitarFret GetFretFromAction(GuitarAction action)
        {
            return action switch
            {
                GuitarAction.Black1Fret => SixFretGuitarFret.Black1,
                GuitarAction.Black2Fret => SixFretGuitarFret.Black2,
                GuitarAction.Black3Fret => SixFretGuitarFret.Black3,
                GuitarAction.White1Fret => SixFretGuitarFret.White1,
                GuitarAction.White2Fret => SixFretGuitarFret.White2,
                GuitarAction.White3Fret => SixFretGuitarFret.White3,
                _ => SixFretGuitarFret.Black1
            };
        }

        // Record of the most recent time that each BRE lane has been lit up by any of the actions that map to it
        private Dictionary<SixFretGuitarFret, double> _fretToMostRecentTime = new()
        {
            { SixFretGuitarFret.Black1,     0 },
            { SixFretGuitarFret.White1,     0 },
            { SixFretGuitarFret.Black2,     0 },
            { SixFretGuitarFret.White2,     0 },
            { SixFretGuitarFret.Black3,     0 },
            { SixFretGuitarFret.White3,     0 },
        };


        // Key is a SixFretGuitarFret
        // Value is the fret's lateral position on the fret array
        private Dictionary<int, int> _lanePositions;

        private float GetLanePositionOrCentered(int fret)
        {
            if (_lanePositions.ContainsKey(fret))
            {
                return _lanePositions[fret];
            }

            return (LaneCount - 1) / 2;
        }

        protected virtual SixFretGuitarFret GetFretIndex(GuitarAction action)
        {
            return action switch
            {
                GuitarAction.Black1Fret => SixFretGuitarFret.Black1,
                GuitarAction.Black2Fret => SixFretGuitarFret.Black2,
                GuitarAction.Black3Fret => SixFretGuitarFret.Black3,
                GuitarAction.White1Fret => SixFretGuitarFret.White1,
                GuitarAction.White2Fret => SixFretGuitarFret.White2,
                GuitarAction.White3Fret => SixFretGuitarFret.White3,
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            };
        }

        public int GetLanePosition(SixFretGuitarFret fret)
        {
            return _lanePositions[(int) fret];
        }

        public override bool ShouldUpdateInputsOnResume => true;

        /// See <see cref="StarMultiplierThresholds"/>
        private static float[] GuitarStarMultiplierThresholds => new[]
        {
            0.06f, 0.12f, 0.2f, 0.47f, 0.78f, 1.15f
        };

        /// See <see cref="StarMultiplierThresholds"/>
        private static float[] BassStarMultiplierThresholds => new[]
        {
            0.05f, 0.1f, 0.19f, 0.47f, 0.78f, 1.15f
        };

        public GuitarEngineParameters EngineParams { get; private set; }

        private double TimeFromSpawnToStrikeline => SpawnTimeOffset - (-STRIKE_LINE_POS / NoteSpeed);




        [Header("Six Fret Specific")]
        [SerializeField]
        private FretArray _fretArray;

        protected override float[] StarMultiplierThresholds { get; set; } =
            GuitarStarMultiplierThresholds;

        public float WhammyFactor { get; private set; }

        private int _sustainCount;

        private SongStem _stem;

        public override void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView, StemMixer mixer, int? currentHighScore)
        {
            _stem = player.Profile.CurrentInstrument.ToSongStem();
            if (_stem == SongStem.Bass && mixer[SongStem.Bass] == null)
            {
                _stem = SongStem.Rhythm;
            }

            BRELanes = new LaneElement[LaneCount];
            // LaneCount is set by the base class, no need to assign here

            base.Initialize(index, player, chart, trackView, mixer, currentHighScore);
        }

        protected override InstrumentDifficulty<GuitarNote> GetNotes(SongChart chart)
        {
            var track = chart.GetSixFretTrack(Player.Profile.CurrentInstrument).Clone();
            return track.GetDifficulty(Player.Profile.CurrentDifficulty);
        }

        protected override GuitarEngine CreateEngine()
        {
            // If on bass, replace the star multiplier threshold
            bool isBass = Player.Profile.CurrentInstrument == Instrument.SixFretBass;
            if (isBass)
            {
                StarMultiplierThresholds = BassStarMultiplierThresholds;
            }

            if (!Player.IsReplay)
            {
                // Create the engine params from the engine preset
                EngineParams = Player.EnginePreset.SixFretGuitar.Create(StarMultiplierThresholds, SoloBonusStarMultiplierThresholds, isBass);
                //EngineParams = EnginePreset.Precision.SixFretGuitar.Create(StarMultiplierThresholds, isBass);
            }
            else
            {
                // Otherwise, get from the replay
                EngineParams = (GuitarEngineParameters) Player.EngineParameterOverride;
            }

            if (EngineContainer != null)
            {
                GameManager.EngineManager.Unregister(EngineContainer);
                EngineContainer = null;
            }

            var engine = new YargSixFretGuitarEngine(NoteTrack, SyncTrack, EngineParams, Player.Profile.IsBot);
            EngineContainer = GameManager.EngineManager.Register(engine, NoteTrack.Instrument, Chart, Player.RockMeterPreset);

            HitWindow = EngineParams.HitWindow;

            YargLogger.LogFormatDebug("Note count: {0}", NoteTrack.Notes.Count);

            engine.OnNoteHit += OnNoteHit;
            engine.OnNoteMissed += OnNoteMissed;
            engine.OnOverstrum += OnOverhit;

            engine.OnSustainStart += OnSustainStart;
            engine.OnSustainEnd += OnSustainEnd;

            engine.OnSoloStart += OnSoloStart;
            engine.OnSoloEnd += OnSoloEnd;

            engine.OnCodaStart += OnCodaStart;
            engine.OnCodaEnd += OnCodaEnd;

            engine.OnStarPowerPhraseHit += OnStarPowerPhraseHit;
            engine.OnStarPowerPhraseMissed += OnStarPowerPhraseMissed;
            engine.OnStarPowerStatus += OnStarPowerStatus;

            engine.OnCountdownChange += OnCountdownChange;

            return engine;
        }

        protected override void FinishInitialization()
        {
            base.FinishInitialization();

            MakeHighwayOrdering();

            IndicatorStripes.Initialize(Player.EnginePreset.SixFretGuitar);


            _fretArray.Initialize(
                _lanePositions,
                LaneCount,
                null,
                Player.ColorProfile.SixFretGuitar,
                Player.ThemePreset,
                VisualStyle.SixFretGuitar
            );

            // 6-fret doesn't use range shift indicators
            // _allRangeShiftEvents = SixFretRangeShift.GetRangeShiftEvents(NoteTrack);
            // InitializeRangeShift();

            LaneElement.DefineLaneScale(Player.Profile.CurrentInstrument, 6);

            GameManager.BeatEventHandler.Visual.Subscribe(_fretArray.PulseFretColors, BeatEventType.StrongBeat);
        }

        public override void ResetPracticeSection()
        {
            base.ResetPracticeSection();

            _fretArray.ResetAll();
        }

        public override void SetPracticeSection(uint start, uint end)
        {
            base.SetPracticeSection(start, end);
        }

        protected override void ResetLastHitTimes()
        {
            foreach (var fret in _lanePositions.Keys)
            {
                _fretToMostRecentTime[(SixFretGuitarFret) fret] = 0;
            }
        }

        public override void SetReplayTime(double time)
        {
            base.SetReplayTime(time);
        }

        protected override void UpdateVisuals(double visualTime)
        {
            // Update coda lane emissions if necessary
            if (Engine.IsCodaActive)
            {
                // Set emission color of BRE lanes depending on currently available score value
                foreach (var (breLaneIndex, highwayOrderingIndex) in _lanePositions)
                {
                    var mostRecentTime = _fretToMostRecentTime[(SixFretGuitarFret) breLaneIndex];
                    BRELanes[highwayOrderingIndex].SetEmissionColor(CodaSection.GetNormalizedTimeSinceLastHit(visualTime, mostRecentTime));
                }
            }

            base.UpdateVisuals(visualTime);
            UpdateFretArray();
        }

        private void UpdateFretArray()
        {
            for (var action = GuitarAction.Black1Fret; action <= GetFretActionMax(); action++)
            {
                _fretArray.SetPressed((int) GetFretIndex(action), Engine.IsFretHeld(action));
            }
        }

        protected virtual GuitarAction GetFretActionMax() => GuitarAction.White3Fret;

        public override void SetStemMuteState(bool muted)
        {
            if (IsStemMuted != muted)
            {
                GameManager.ChangeStemMuteState(_stem, muted);
                IsStemMuted = muted;
            }
        }

        public override void SetStarPowerFX(bool active)
        {
            GameManager.ChangeStemReverbState(_stem, active);
        }

        protected override void ResetVisuals()
        {
            base.ResetVisuals();

            _fretArray.ResetAll();
        }

        protected override void InitializeSpawnedNote(IPoolable poolable, GuitarNote note)
        {
            var element = (SixFretGuitarNoteElement) poolable;
            element.NoteRef = note;

            if (!Player.Profile.SixFretSplitLanes &&
                note.Fret != (int) SixFretGuitarFret.Open &&
                note.Fret != (int) SixFretGuitarFret.Wildcard)
            {
                element.IsPaired = FindPairInChord(note);
            }
            else
            {
                element.IsPaired = false;
            }
        }

        private bool FindPairInChord(GuitarNote note)
        {
            int pairIdx = GetPairIndex((SixFretGuitarFret) note.Fret);

            // Use ParentOrSelf to get full chord; child notes have empty ChildNotes
            foreach (var other in note.ParentOrSelf.AllNotes)
            {
                if (other == note) continue;
                int otherFret = other.Fret;
                if (otherFret == (int) SixFretGuitarFret.Open ||
                    otherFret == (int) SixFretGuitarFret.Wildcard) continue;
                if (otherFret == note.Fret) continue;

                if (GetPairIndex((SixFretGuitarFret) otherFret) == pairIdx)
                {
                    return true;
                }
            }
            return false;
        }

        protected override void InitializeSpawnedLane(LaneElement lane, GuitarNote note)
        {
            lane.SetAppearance(
                Player.Profile.CurrentInstrument,
                note.LaneNote,
                GetLanePositionOrCentered(note.Fret),
                LaneCount,
                Player.ColorProfile.SixFretGuitar.GetNoteColor(note.Fret).ToUnityColor()
            );

            if (!Player.Profile.SixFretSplitLanes &&
                note.Fret != (int) SixFretGuitarFret.Open &&
                note.Fret != (int) SixFretGuitarFret.Wildcard &&
                !FindPairInChord(note))
            {
                lane.SetCombinedSpan(true);
            }
        }

        protected override void InitializeSpawnedLane(LaneElement lane, int laneIndex)
        {
            var index = Player.Profile.LeftyFlip ? (LaneCount - 1) - laneIndex : laneIndex;
            lane.SetAppearance(
                Player.Profile.CurrentInstrument,
                laneIndex,
                laneIndex,
                LaneCount,
                Player.ColorProfile.SixFretGuitar.GetNoteColor(index + 1).ToUnityColor());
        }

        protected override void ModifyLaneFromNote(LaneElement lane, GuitarNote note)
        {
            if (note.Fret == (int) SixFretGuitarFret.Open)
            {
                lane.ToggleOpen(true);
            }
            else
            {
                lane.MultiplyScale(0.85f);
            }
        }

        protected override void RescaleLanesForBRE()
        {
            LaneElement.DefineLaneScale(Player.Profile.CurrentInstrument, 6, true);
        }

        private void OnLaneHit(int action)
        {
            var asFret = GetFretFromAction((GuitarAction) action);

            _fretToMostRecentTime[asFret] = GameManager.VisualTime;
            _fretArray.PlayCodaHitAnimation((int) asFret);
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

        protected override void OnNoteHit(int index, GuitarNote chordParent)
        {
            base.OnNoteHit(index, chordParent);

            if (GameManager.Paused) return;

            foreach (var note in chordParent.AllNotes)
            {
                (NotePool.GetByKey(note) as SixFretGuitarNoteElement)?.HitNote();

                if (note.Fret != (int) SixFretGuitarFret.Open && note.Fret != (int) SixFretGuitarFret.Wildcard)
                {
                    _fretArray.PlayHitAnimation(note.Fret);
                }
                else
                {
                    _fretArray.PlayOpenHitAnimation();
                }
            }
        }

        protected override void OnNoteMissed(int index, GuitarNote chordParent)
        {
            base.OnNoteMissed(index, chordParent);

            foreach (var note in chordParent.AllNotes)
            {
                (NotePool.GetByKey(note) as SixFretGuitarNoteElement)?.MissNote();
            }
        }

        protected override void OnOverhit()
        {
            base.OnOverhit();

            if (GameManager.IsSeekingReplay)
            {
                return;
            }

            if (SettingsManager.Settings.OverstrumAndOverhitSoundEffects.Value)
            {
                const int MIN = (int) SfxSample.Overstrum1;
                const int MAX = (int) SfxSample.Overstrum4;

                var randomOverstrum = (SfxSample) Random.Range(MIN, MAX + 1);
                GlobalAudioHandler.PlaySoundEffect(randomOverstrum);
            }

            // To check if held frets are valid
            GuitarNote currentNote = null;
            if (Engine.NoteIndex < Notes.Count)
            {
                var note = Notes[Engine.NoteIndex];

                // Don't take the note if it's not within the hit window
                // TODO: Make BaseEngine.IsNoteInWindow public and use that instead
                var (frontEnd, backEnd) = Engine.CalculateHitWindow();
                if (Engine.CurrentTime >= (note.Time + frontEnd) && Engine.CurrentTime <= (note.Time + backEnd))
                {
                    currentNote = note;
                }
            }

            // Play miss animation for every held fret that does not match the current note
            bool anyHeld = false;
            for (var action = GuitarAction.Black1Fret; action <= GetFretActionMax(); action++)
            {
                if (!Engine.IsFretHeld(action))
                {
                    continue;
                }

                anyHeld = true;

                if (currentNote == null || (currentNote.NoteMask & (1 << (int) action)) == 0)
                {
                    _fretArray.PlayMissAnimation((int) GetFretIndex(action));
                }
            }

            // Play open-strum miss if no frets are held
            if (!anyHeld)
            {
                _fretArray.PlayOpenMissAnimation();
            }
        }

        private void OnSustainStart(GuitarNote parent)
        {
            foreach (var note in parent.AllNotes)
            {
                // If the note is disjoint, only iterate the parent as sustains are added separately
                if (parent.IsDisjoint && parent != note)
                {
                    continue;
                }

                if (note.Fret != (int) SixFretGuitarFret.Open && note.Fret != (int) SixFretGuitarFret.Wildcard)
                {
                    _fretArray.SetSustained(note.Fret, true);
                }

                _sustainCount++;
            }
        }

        private void OnSustainEnd(GuitarNote parent, double timeEnded, bool finished)
        {
            foreach (var note in parent.AllNotes)
            {
                // If the note is disjoint, only iterate the parent as sustains are added separately
                if (parent.IsDisjoint && parent != note)
                {
                    continue;
                }

                (NotePool.GetByKey(note) as SixFretGuitarNoteElement)?.SustainEnd(finished);

                if (note.Fret != (int) SixFretGuitarFret.Open && note.Fret != (int) SixFretGuitarFret.Wildcard)
                {
                    _fretArray.SetSustained(note.Fret, false);
                }

                _sustainCount--;
            }

            // Mute the stem if you let go of the sustain too early.
            // Leniency is handled by the engine's sustain burst threshold.
            if (!finished)
            {
                if (!parent.IsDisjoint || _sustainCount == 0)
                {
                    SetStemMuteState(true);
                }
            }

            if (_sustainCount == 0)
            {
                WhammyFactor = 0;
                GameManager.ChangeStemWhammyPitch(_stem, 0);
            }
        }

        protected override void OnStarPowerPhraseMissed()
        {
            base.OnStarPowerPhraseMissed();
            foreach (var note in NotePool.AllSpawned)
            {
                (note as SixFretGuitarNoteElement)?.OnStarPowerUpdated();
            }
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            // Ignore SP in practice mode
            if (input.GetAction<GuitarAction>() == GuitarAction.StarPower && GameManager.IsPractice) return true;

            return false;
        }

        protected override void OnInputQueued(GameInput input)
        {
            base.OnInputQueued(input);

            // Update the whammy factor
            if (_sustainCount > 0 && input.GetAction<GuitarAction>() == GuitarAction.Whammy)
            {
                WhammyFactor = Mathf.Clamp01(input.Axis);
                GameManager.ChangeStemWhammyPitch(_stem, WhammyFactor);
            }
        }

        public override (ReplayFrame Frame, ReplayStats Stats) ConstructReplayData()
        {
            var frame = new ReplayFrame(Player.Profile, EngineParams, Engine.EngineStats, ReplayInputs.ToArray());
            return (frame, Engine.EngineStats.ConstructReplayStats(Player.Profile.Name));
        }


        private void MakeHighwayOrdering()
        {
            if (Player.Profile.LeftyFlip)
            {
                _lanePositions = new()
                {
                    { (int)SixFretGuitarFret.White3,     0 },
                    { (int)SixFretGuitarFret.Black3,     1 },
                    { (int)SixFretGuitarFret.White2,     2 },
                    { (int)SixFretGuitarFret.Black2,     3 },
                    { (int)SixFretGuitarFret.White1,     4 },
                    { (int)SixFretGuitarFret.Black1,     5 }
                };
            }
            else
            {
                _lanePositions = DEFAULT_HIGHWAY_ORDERING;
            }
        }
    }
}

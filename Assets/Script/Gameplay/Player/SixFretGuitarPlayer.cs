using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Themes;

namespace YARG.Gameplay.Player
{
    public class SixFretGuitarPlayer : FiveFretGuitarPlayer
    {
        // Combined lane pair index: (Black1,White1)->0, (Black2,White2)->1, (Black3,White3)->2
        private static readonly Dictionary<SixFretGuitarFret, int> COMBINED_PAIR_INDEX = new()
        {
            { SixFretGuitarFret.Black1, 0 }, { SixFretGuitarFret.White1, 0 },
            { SixFretGuitarFret.Black2, 1 }, { SixFretGuitarFret.White2, 1 },
            { SixFretGuitarFret.Black3, 2 }, { SixFretGuitarFret.White3, 2 },
        };

        private static int GetPairIndex(SixFretGuitarFret fret) => COMBINED_PAIR_INDEX[fret];

        public new static Dictionary<int, int> DEFAULT_HIGHWAY_ORDERING { get; } = new()
        {
            { (int)SixFretGuitarFret.Black1, 0 },
            { (int)SixFretGuitarFret.White1, 1 },
            { (int)SixFretGuitarFret.Black2, 2 },
            { (int)SixFretGuitarFret.White2, 3 },
            { (int)SixFretGuitarFret.Black3, 4 },
            { (int)SixFretGuitarFret.White3, 5 },
        };

        public new int LaneCount => 6;

        protected override int GetFretIndex(GuitarAction action)
        {
            return (int)(action switch
            {
                GuitarAction.Black1Fret => SixFretGuitarFret.Black1,
                GuitarAction.Black2Fret => SixFretGuitarFret.Black2,
                GuitarAction.Black3Fret => SixFretGuitarFret.Black3,
                GuitarAction.White1Fret => SixFretGuitarFret.White1,
                GuitarAction.White2Fret => SixFretGuitarFret.White2,
                GuitarAction.White3Fret => SixFretGuitarFret.White3,
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            });
        }

        protected override GuitarAction GetFretActionMax() => GuitarAction.White3Fret;

        protected override Dictionary<int, double> CreateFretToMostRecentTime() => new()
        {
            { (int)SixFretGuitarFret.Black1, 0 },
            { (int)SixFretGuitarFret.White1, 0 },
            { (int)SixFretGuitarFret.Black2, 0 },
            { (int)SixFretGuitarFret.White2, 0 },
            { (int)SixFretGuitarFret.Black3, 0 },
            { (int)SixFretGuitarFret.White3, 0 },
        };

        public int GetLanePosition(SixFretGuitarFret fret)
        {
            return _lanePositions[(int)fret];
        }

        protected override InstrumentDifficulty<GuitarNote> GetNotes(SongChart chart)
        {
            var track = chart.GetSixFretTrack(Player.Profile.CurrentInstrument).Clone();
            return track.GetDifficulty(Player.Profile.CurrentDifficulty);
        }

        protected override GuitarEngine CreateEngine()
        {
            bool isBass = Player.Profile.CurrentInstrument == Instrument.SixFretBass;
            if (isBass)
            {
                StarMultiplierThresholds = new[] { 0.05f, 0.1f, 0.19f, 0.47f, 0.78f, 1.15f };
            }

            if (!Player.IsReplay)
            {
                EngineParams = Player.EnginePreset.SixFretGuitar.Create(StarMultiplierThresholds, SoloBonusStarMultiplierThresholds, isBass);
            }
            else
            {
                EngineParams = (GuitarEngineParameters)Player.EngineParameterOverride;
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

        protected override void InitializeIndicatorStripes()
        {
            IndicatorStripes.Initialize(Player.EnginePreset.SixFretGuitar);
        }

        protected override void InitializeFretArray()
        {
            _fretArray.Initialize(
                _lanePositions,
                LaneCount,
                null,
                Player.ColorProfile.SixFretGuitar,
                Player.ThemePreset,
                VisualStyle.SixFretGuitar
            );
        }

        protected override void InitializeSpawnedNote(IPoolable poolable, GuitarNote note)
        {
            var element = (SixFretGuitarNoteElement)poolable;
            element.NoteRef = note;

            if (!Player.Profile.SixFretSplitLanes &&
                note.Fret != (int)SixFretGuitarFret.Open &&
                note.Fret != (int)SixFretGuitarFret.Wildcard)
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
            int pairIdx = GetPairIndex((SixFretGuitarFret)note.Fret);

            foreach (var other in note.ParentOrSelf.AllNotes)
            {
                if (other == note) continue;
                int otherFret = other.Fret;
                if (otherFret == (int)SixFretGuitarFret.Open ||
                    otherFret == (int)SixFretGuitarFret.Wildcard) continue;
                if (otherFret == note.Fret) continue;

                if (GetPairIndex((SixFretGuitarFret)otherFret) == pairIdx)
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
                note.Fret != (int)SixFretGuitarFret.Open &&
                note.Fret != (int)SixFretGuitarFret.Wildcard &&
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
            if (note.Fret == (int)SixFretGuitarFret.Open)
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

        protected override void OnNoteHit(int index, GuitarNote chordParent)
        {
            base.OnNoteHit(index, chordParent);

            if (GameManager.Paused) return;

            foreach (var note in chordParent.AllNotes)
            {
                (NotePool.GetByKey(note) as SixFretGuitarNoteElement)?.HitNote();

                if (note.Fret != (int)SixFretGuitarFret.Open && note.Fret != (int)SixFretGuitarFret.Wildcard)
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

        private void OnSustainStart(GuitarNote parent)
        {
            foreach (var note in parent.AllNotes)
            {
                if (parent.IsDisjoint && parent != note) continue;

                if (note.Fret != (int)SixFretGuitarFret.Open && note.Fret != (int)SixFretGuitarFret.Wildcard)
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
                if (parent.IsDisjoint && parent != note) continue;

                (NotePool.GetByKey(note) as SixFretGuitarNoteElement)?.SustainEnd(finished);

                if (note.Fret != (int)SixFretGuitarFret.Open && note.Fret != (int)SixFretGuitarFret.Wildcard)
                {
                    _fretArray.SetSustained(note.Fret, false);
                }

                _sustainCount--;
            }

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

        protected override void OnInputQueued(GameInput input)
        {
            base.OnInputQueued(input);

            if (_sustainCount > 0 && input.GetAction<GuitarAction>() == GuitarAction.Whammy)
            {
                WhammyFactor = Mathf.Clamp01(input.Axis);
                GameManager.ChangeStemWhammyPitch(_stem, WhammyFactor);
            }
        }

        protected override void MakeHighwayOrdering()
        {
            if (Player.Profile.LeftyFlip)
            {
                _lanePositions = new()
                {
                    { (int)SixFretGuitarFret.White3, 0 },
                    { (int)SixFretGuitarFret.Black3, 1 },
                    { (int)SixFretGuitarFret.White2, 2 },
                    { (int)SixFretGuitarFret.Black2, 3 },
                    { (int)SixFretGuitarFret.White1, 4 },
                    { (int)SixFretGuitarFret.Black1, 5 },
                };
            }
            else
            {
                _lanePositions = DEFAULT_HIGHWAY_ORDERING;
            }
        }
    }
}

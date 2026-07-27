using System.Collections.Generic;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Game;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Themes;

namespace YARG.Gameplay.Player
{
    public class SixFretGuitarPlayer : FiveFretGuitarPlayer
    {
        // Lane mapping: 3 visual lanes, each = black+white pair
        public static Dictionary<int, int> DEFAULT_LANE_POSITIONS { get; } = new()
        {
            { (int)SixFretGuitarFret.Black1, 0 },
            { (int)SixFretGuitarFret.White1, 0 },
            { (int)SixFretGuitarFret.Black2, 1 },
            { (int)SixFretGuitarFret.White2, 1 },
            { (int)SixFretGuitarFret.Black3, 2 },
            { (int)SixFretGuitarFret.White3, 2 },
        };

        public new int LaneCount => 3;

        // Determine if fret is "up" row (black normal, white lefty flip)
        protected bool IsUpFret(SixFretGuitarFret fret)
        {
            return Player.Profile.LeftyFlip
                ? fret is >= SixFretGuitarFret.White1 and <= SixFretGuitarFret.White3
                : fret is >= SixFretGuitarFret.Black1 and <= SixFretGuitarFret.Black3;
        }

        // Get lane index (0-2) for a fret (accessible to note elements)
        public int GetLaneIndex(SixFretGuitarFret fret) => _lanePositions[(int)fret];

        protected override int GetFretIndex(GuitarAction action)
        {
            return action switch
            {
                GuitarAction.Black1Fret => (int)SixFretGuitarFret.Black1,
                GuitarAction.Black2Fret => (int)SixFretGuitarFret.Black2,
                GuitarAction.Black3Fret => (int)SixFretGuitarFret.Black3,
                GuitarAction.White1Fret => (int)SixFretGuitarFret.White1,
                GuitarAction.White2Fret => (int)SixFretGuitarFret.White2,
                GuitarAction.White3Fret => (int)SixFretGuitarFret.White3,
                _ => base.GetFretIndex(action)
            };
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

        // --- CreateEngine() hooks (replaces full method duplication) ---

        protected override Instrument GetBassInstrument() => Instrument.SixFretBass;

        protected override EnginePreset.FiveFretGuitarPreset GetEnginePreset() => Player.EnginePreset.SixFretGuitar;

        protected override GuitarEngine BuildEngine(GuitarEngineParameters parameters)
            => new YargSixFretGuitarEngine(NoteTrack, SyncTrack, parameters, Player.Profile.IsBot);

        // --- End CreateEngine() hooks ---

        protected override void InitializeIndicatorStripes()
        {
            IndicatorStripes.Initialize(Player.EnginePreset.SixFretGuitar);
        }

        protected override void InitializeFretArray()
        {
            _fretArray.Initialize(
                _lanePositions,
                3,  // 3 visual lanes
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

            // Open/Wildcard: no lane type (full-width)
            if (note.Fret == (int)SixFretGuitarFret.Open ||
                note.Fret == (int)SixFretGuitarFret.Wildcard)
            {
                element.LaneType = SixFretGuitarNoteElement.LaneNoteType.None;
                return;
            }

            // Check if sibling note exists in same lane pair (barre)
            bool hasSibling = false;
            foreach (var other in note.ParentOrSelf.AllNotes)
            {
                if (other != note &&
                    other.Fret != (int)SixFretGuitarFret.Open &&
                    other.Fret != (int)SixFretGuitarFret.Wildcard &&
                    GetLaneIndex((SixFretGuitarFret)other.Fret) == GetLaneIndex((SixFretGuitarFret)note.Fret) &&
                    other.Fret != note.Fret)
                {
                    hasSibling = true;
                    break;
                }
            }

            if (hasSibling)
            {
                element.LaneType = SixFretGuitarNoteElement.LaneNoteType.Barre;
            }
            else if (IsUpFret((SixFretGuitarFret)note.Fret))
            {
                element.LaneType = SixFretGuitarNoteElement.LaneNoteType.Up;
            }
            else
            {
                element.LaneType = SixFretGuitarNoteElement.LaneNoteType.Down;
            }
        }

        protected override void InitializeSpawnedLane(LaneElement lane, GuitarNote note)
        {
            int laneIndex = GetLaneIndex((SixFretGuitarFret)note.Fret);
            lane.SetAppearance(
                Player.Profile.CurrentInstrument,
                note.LaneNote,
                laneIndex,
                3,  // 3 visual lanes
                Player.ColorProfile.SixFretGuitar.GetNoteColor(note.Fret).ToUnityColor()
            );
        }

        protected override void InitializeSpawnedLane(LaneElement lane, int laneIndex)
        {
            // Map laneIndex (0-2) to corresponding fret color
            var fret = (SixFretGuitarFret)(laneIndex + 1); // Black1, Black2, Black3
            if (Player.Profile.LeftyFlip)
            {
                fret = laneIndex switch
                {
                    0 => SixFretGuitarFret.Black3,
                    1 => SixFretGuitarFret.Black2,
                    2 => SixFretGuitarFret.Black1,
                    _ => fret
                };
            }
            lane.SetAppearance(
                Player.Profile.CurrentInstrument,
                laneIndex,
                laneIndex,
                3,  // 3 visual lanes
                Player.ColorProfile.SixFretGuitar.GetNoteColor((int)fret).ToUnityColor());
        }

        protected override void ModifyLaneFromNote(LaneElement lane, GuitarNote note)
        {
            if (note.Fret == (int)SixFretGuitarFret.Open)
            {
                lane.ToggleFullWidth(true);
            }
            else
            {
                lane.MultiplyScale(0.85f);
            }
        }

        protected override void RescaleLanesForBRE()
        {
            LaneElement.DefineLaneScale(Player.Profile.CurrentInstrument, 3, true);
        }

        protected override void UpdateFretArray()
        {
            // Iterate lane pairs (0, 1, 2)
            for (int pair = 0; pair < 3; pair++)
            {
                var blackFret = (SixFretGuitarFret)(pair + 1); // Black1=1, Black2=2, Black3=3
                var whiteFret = (SixFretGuitarFret)(pair + 4); // White1=4, White2=5, White3=6
                var blackHeld = Engine.IsFretHeld((GuitarAction)(int)blackFret);
                var whiteHeld = Engine.IsFretHeld((GuitarAction)(int)whiteFret);
                var fretIndex = (int)blackFret; // Matches _frets dict key (Black1=1)

                if (blackHeld && whiteHeld)
                {
                    // Both pressed — light both halves
                    _fretArray.SetPressed(fretIndex, true);
                    _fretArray.SetPressedSecondary(fretIndex, true);
                }
                else if (blackHeld)
                {
                    // Black only — primary half
                    _fretArray.SetPressed(fretIndex, true);
                    _fretArray.SetPressedSecondary(fretIndex, false);
                }
                else if (whiteHeld)
                {
                    // White only — secondary half
                    _fretArray.SetPressed(fretIndex, false);
                    _fretArray.SetPressedSecondary(fretIndex, true);
                }
                else
                {
                    // Neither — both off
                    _fretArray.SetPressed(fretIndex, false);
                    _fretArray.SetPressedSecondary(fretIndex, false);
                }
            }
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

        protected override void OnSustainStart(GuitarNote parent)
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

        protected override void OnSustainEnd(GuitarNote parent, double timeEnded, bool finished)
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

        protected override void MakeHighwayOrdering()
        {
            if (Player.Profile.LeftyFlip)
            {
                // Swap lane 0 ↔ 2, keep lane 1 centered
                _lanePositions = new()
                {
                    { (int)SixFretGuitarFret.Black1, 2 },
                    { (int)SixFretGuitarFret.White1, 2 },
                    { (int)SixFretGuitarFret.Black2, 1 },
                    { (int)SixFretGuitarFret.White2, 1 },
                    { (int)SixFretGuitarFret.Black3, 0 },
                    { (int)SixFretGuitarFret.White3, 0 },
                };
            }
            else
            {
                _lanePositions = new Dictionary<int, int>(DEFAULT_LANE_POSITIONS);
            }
        }
    }
}

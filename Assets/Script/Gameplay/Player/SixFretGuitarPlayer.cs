using System;
using System.Collections.Generic;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Game;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Themes;

namespace YARG.Gameplay.Player
{
    //TODO: This needs to be decoupled from FiveFretGuitarPlayer, as it is not a 5-fret instrument.
    // Either make a base GuitarPlayer class, or make a new SixFretGuitarPlayer class that does not inherit from FiveFretGuitarPlayer.
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

        // Determine if fret is "up" row (black normal, white lefty flip)
        protected bool IsUpFret(SixFretGuitarFret fret)
        {
            return Player.Profile.LeftyFlip
                ? fret is >= SixFretGuitarFret.White1 and <= SixFretGuitarFret.White3
                : fret is >= SixFretGuitarFret.Black1 and <= SixFretGuitarFret.Black3;
        }

        // Get lane index (0-2) for a fret (accessible to note elements)
        public int GetLaneIndex(SixFretGuitarFret fret) => HighwayOrdering[(int)fret];

        protected override int GetFretIndex(int action)
        {
            return action switch
            {
                (int)GuitarAction.Black1Fret => (int)SixFretGuitarFret.Black1,
                (int)GuitarAction.Black2Fret => (int)SixFretGuitarFret.Black2,
                (int)GuitarAction.Black3Fret => (int)SixFretGuitarFret.Black3,
                (int)GuitarAction.White1Fret => (int)SixFretGuitarFret.White1,
                (int)GuitarAction.White2Fret => (int)SixFretGuitarFret.White2,
                (int)GuitarAction.White3Fret => (int)SixFretGuitarFret.White3,
                YargFiveFretGuitarEngine.OPEN_BRE_INPUT => (int)SixFretGuitarFret.Open,
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

        protected override InstrumentDifficulty<GuitarNote> GetNotes(SongChart chart)
        {
            // 5-fret charts played in 6-fret mode are remapped into legal 6-fret chords;
            // lefty flip additionally swaps the black/white pad rows of every note
            var instrumentDifficulty = chart.GetSixFretPlayableDifficulty(Player.Profile.CurrentInstrument, Player.Profile.CurrentDifficulty,
                Player.Profile.LeftyFlip);
            if (instrumentDifficulty.Instrument is Instrument.FiveFretBass or Instrument.FiveFretGuitar)
            {
                _chartIsFiveFretOnSixFret = true;
            }

            return instrumentDifficulty;
        }

        // --- CreateEngine() hooks (replaces full method duplication) ---
        private bool _chartIsFiveFretOnSixFret = false;

        //TODO: We need a better way to represent 5F charts on 6F, or any other converted chart than just pretending they are the chart's instrument.
        protected override Instrument GetBassInstrument() => _chartIsFiveFretOnSixFret ? Instrument.FiveFretBass : Instrument.SixFretBass;

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
                HighwayOrdering,
                LaneCount,  // 3 visual lanes
                null,
                Player.ColorProfile.SixFretGuitar,
                Player.ThemePreset,
                VisualStyle.SixFretGuitar,
                true  // dualHalfFrets: black/white pairs share a fret object
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
            lane.SetAppearance(
                Player.Profile.CurrentInstrument,
                GetLaneIndex((SixFretGuitarFret)note.Fret),
                GetLanePositionOrCentered(note.Fret),
                LaneCount,
                Player.ColorProfile.SixFretGuitar.GetNoteColor(note.Fret).ToUnityColor()
            );
        }

        protected override void InitializeBRELane(LaneElement lane, int laneIndex)
        {
            int lanedFret = -1;

            foreach (var (fret, position) in HighwayOrdering)
            {
                if (position == laneIndex)
                {
                    lanedFret = fret;
                    break;
                }
            }

            if (lanedFret == -1)
            {
                YargLogger.LogError("Tried to make a BRE lane for a fret with no highway position.");
                return;
            }


            lane.SetAppearance(
                Player.Profile.CurrentInstrument,
                lanedFret,
                laneIndex,
                3,
                Player.ColorProfile.SixFretGuitar.GetNoteColor(lanedFret).ToUnityColor());
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

        /// <summary>
        /// Six-fret BRE lanes cover a black/white fret pair, so the lane is lit by the
        /// most recent press of either fret and glows with the white preset color.
        /// </summary>
        protected override void UpdateBreLaneEmissions(double visualTime)
        {
            var emissionColor = Player.ColorProfile.SixFretGuitar.GetNoteColor((int)SixFretGuitarFret.White1).ToUnityColor();

            for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
            {
                double mostRecentTime = 0;
                foreach (var (fret, lanePosition) in HighwayOrdering)
                {
                    if (lanePosition == laneIndex)
                    {
                        mostRecentTime = Math.Max(mostRecentTime, FretToMostRecentTime[fret]);
                    }
                }

                var normalizedTimeSinceLastHit = CodaSection.GetNormalizedTimeSinceLastHit(visualTime, mostRecentTime);
                BRELanes[laneIndex].SetEmissionColor(emissionColor, normalizedTimeSinceLastHit);
            }
        }

        protected override void OnCodaStart(CodaSection coda)
        {
            base.OnCodaStart(coda);

            // 6-fret condenses its 6 fret buttons into 3 BRE lanes (one per fret number)
            CurrentCoda.SetLaneIndexes(new()
            {
                { 0, 0 }, // B1
                { 1, 1 }, // B2
                { 2, 2 }, // B3
                { 3, 0 }, // W1
                { 4, 1 }, // W2
                { 5, 2 }, // W3
            });
        }

        protected override void UpdateFretArray()
        {
            // Iterate lane pairs (0, 1, 2)
            for (int pair = 0; pair < LaneCount; pair++)
            {
                var blackFret = (SixFretGuitarFret)(pair + 1); // Black1=1, Black2=2, Black3=3
                var whiteFret = (SixFretGuitarFret)(pair + 4); // White1=4, White2=5, White3=6
                var blackHeld = Engine.IsFretHeld((GuitarAction)((int)blackFret - 1));
                var whiteHeld = Engine.IsFretHeld((GuitarAction)((int)whiteFret - 1));
                var fretIndex = (int)blackFret; // Matches _frets dict key (Black1=1)

                _fretArray.SetPressed(fretIndex, blackHeld);
                _fretArray.SetPressedSecondary(fretIndex, whiteHeld);
            }
        }

        protected override void OnNoteHit(int index, GuitarNote chordParent)
        {
            base.OnNoteHit(index, chordParent);

            if (GameManager.Paused) return;

            foreach (var note in chordParent.AllNotes)
            {
                (NotePool.GetByKey(note) as SixFretGuitarNoteElement)?.HitNote();
            }

            // Color the fret hit particles based on which notes were hit:
            // black particles for black frets, white for white frets, a mix of
            // the two for barres (both halves of a pad), and the open particles
            // for open/wild notes. One burst per fret pad, not per note.
            var colors = Player.ColorProfile.SixFretGuitar;
            var blackParticles = colors.BlackParticles.ToUnityColor();
            var whiteParticles = colors.WhiteParticles.ToUnityColor();

            bool openHit = false;
            int blackLanes = 0;
            int whiteLanes = 0;

            foreach (var note in chordParent.AllNotes)
            {
                if (note.Fret == (int) SixFretGuitarFret.Open || note.Fret == (int) SixFretGuitarFret.Wildcard)
                {
                    openHit = true;
                    continue;
                }

                // Fret-pair index (0-2), independent of lefty flip; fret objects
                // are keyed by fret value, not visual lane
                int pair = note.Fret <= (int) SixFretGuitarFret.Black3
                    ? note.Fret - (int) SixFretGuitarFret.Black1
                    : note.Fret - (int) SixFretGuitarFret.White1;
                if (note.Fret <= (int) SixFretGuitarFret.Black3)
                {
                    blackLanes |= 1 << pair;
                }
                else
                {
                    whiteLanes |= 1 << pair;
                }
            }

            if (openHit)
            {
                _fretArray.PlayFullWidthHitAnimation();
            }

            for (int pair = 0; pair < LaneCount; pair++)
            {
                bool black = (blackLanes & (1 << pair)) != 0;
                bool white = (whiteLanes & (1 << pair)) != 0;
                if (!black && !white) continue;

                var particleColor = black && white
                    ? UnityEngine.Color.Lerp(blackParticles, whiteParticles, 0.5f)
                    : black ? blackParticles : whiteParticles;

                // Both halves of a pad share one fret object, keyed by the black fret
                _fretArray.PlayHitAnimation((int) SixFretGuitarFret.Black1 + pair, particleColor);
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
            LaneCount = 3;
            BRELanes = new LaneElement[LaneCount];
            // 6F does not have an open lane setting
            if (Player.Profile.LeftyFlip)
            {
                // Swap lane 0 ↔ 2, keep lane 1 centered
                HighwayOrdering = new()
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
                HighwayOrdering = new Dictionary<int, int>(DEFAULT_LANE_POSITIONS);
            }
        }
    }
}

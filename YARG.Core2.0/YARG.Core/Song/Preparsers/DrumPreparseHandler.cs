using System;
using YARG.Core.Chart;
using YARG.Core.IO;

namespace YARG.Core.Song.Preparsers
{
    public class DrumPreparseHandler
    {
        private DifficultyMask _validations;
        public DrumsType Type;

        public DifficultyMask ValidatedDiffs => _validations;

        public bool ParseChart<TChar>(ref YARGTextContainer<TChar> container, Difficulty difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            var diffMask = difficulty.ToDifficultyMask();
            if ((_validations & diffMask) > 0)
            {
                return false;
            }

            const int YELLOW_CYMBAL = 66;
            const int GREEN_CYMBAL = 68;
            const int DOUBLE_BASS_MODIFIER = 32;

            var requiredMask = diffMask;
            if (difficulty == Difficulty.Expert)
            {
                requiredMask |= DifficultyMask.ExpertPlus;
            }

            DotChartEvent ev = default;
            while (YARGChartFileReader.TryParseEvent(ref container, ref ev))
            {
                if (ev.Type == ChartEventType.Note)
                {
                    int lane = YARGTextReader.ExtractInt32AndWhitespace(ref container);
                    long _ = YARGTextReader.ExtractInt64AndWhitespace(ref container);
                    if (0 <= lane && lane <= 4)
                    {
                        _validations |= diffMask;
                    }
                    else if (lane == 5)
                    {
                        if (Type == DrumsType.FiveLane || Type == DrumsType.Unknown)
                        {
                            Type = DrumsType.FiveLane;
                            _validations |= diffMask;
                        }
                    }
                    else if (YELLOW_CYMBAL <= lane && lane <= GREEN_CYMBAL)
                    {
                        if (Type != DrumsType.FiveLane)
                        {
                            Type = DrumsType.ProDrums;
                        }
                    }
                    else if (lane == DOUBLE_BASS_MODIFIER)
                    {
                        if (difficulty == Difficulty.Expert)
                        {
                            _validations |= DifficultyMask.ExpertPlus;
                        }
                    }

                    //  Testing against zero would not work in expert
                    if ((_validations & requiredMask) == requiredMask && (Type == DrumsType.ProDrums || Type == DrumsType.FiveLane))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static readonly int[] INDICES = new int[MidiPreparser_Constants.NUM_DIFFICULTIES * MidiPreparser_Constants.NOTES_PER_DIFFICULTY]
        {
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
        };

        public unsafe void ParseMidi(YARGMidiTrack track)
        {
            if (_validations > 0)
                return;

            const int MAX_NUMPADS = 7;
            const int DRUMNOTE_MAX = 101;
            const int DOUBLE_KICK_NOTE = 95;
            const int DOUBLE_KICK_MASK = 1 << (3 * MAX_NUMPADS + 1);
            const int FIVE_LANE_INDEX = 6;
            const int YELLOW_FLAG = 110;
            const int GREEN_FLAG = 112;

            int statusBitMask = 0;
            var note = default(MidiNote);
            while (track.ParseEvent())
            {
                if (track.Type != MidiEventType.Note_On && track.Type != MidiEventType.Note_Off)
                {
                    continue;
                }

                track.ExtractMidiNote(ref note);
                // Must be checked first as it still resides in the normal note range window
                if (note.value == DOUBLE_KICK_NOTE)
                {
                    if ((_validations & DifficultyMask.ExpertPlus) > 0)
                    {
                        continue;
                    }

                    // Note Ons with no velocity equates to a note Off by spec
                    if (track.Type == MidiEventType.Note_On && note.velocity > 0)
                    {
                        statusBitMask |= DOUBLE_KICK_MASK;
                    }
                    // NoteOff here
                    else if ((statusBitMask & DOUBLE_KICK_MASK) > 0)
                    {
                        _validations |= DifficultyMask.Expert | DifficultyMask.ExpertPlus;
                    }
                }
                else if (MidiPreparser_Constants.DEFAULT_NOTE_MIN <= note.value && note.value <= DRUMNOTE_MAX)
                {
                    int noteOffset = note.value - MidiPreparser_Constants.DEFAULT_NOTE_MIN;
                    int diffIndex = MidiPreparser_Constants.DIFF_INDICES[noteOffset];
                    var diffMask = (DifficultyMask) (1 << (diffIndex + 1));
                    //                             Necessary to account for potential five lane
                    if ((_validations & diffMask) > 0 && Type != DrumsType.Unknown && Type != DrumsType.UnknownPro)
                    {
                        continue;
                    }

                    int laneIndex = INDICES[noteOffset];
                    // The double "greater than" check against FIVE_LANE_INDEX keeps the number of comparisons performed
                    // to ONE when laneIndex is less than that value
                    if (laneIndex >= FIVE_LANE_INDEX && (laneIndex > FIVE_LANE_INDEX || Type == DrumsType.FourLane || Type == DrumsType.ProDrums))
                    {
                        continue;
                    }

                    int statusMask = 1 << (diffIndex * MAX_NUMPADS + laneIndex);
                    // Note Ons with no velocity equates to a note Off by spec
                    if (track.Type == MidiEventType.Note_On && note.velocity > 0)
                    {
                        statusBitMask |= statusMask;
                        if (laneIndex == FIVE_LANE_INDEX)
                        {
                            Type = DrumsType.FiveLane;
                        }
                    }
                    // NoteOff here
                    else if ((statusBitMask & statusMask) > 0)
                    {
                        _validations |= diffMask;
                    }
                }
                else if (YELLOW_FLAG <= note.value && note.value <= GREEN_FLAG && Type != DrumsType.FiveLane)
                {
                    Type = DrumsType.ProDrums;
                }

                if (_validations == MidiPreparser_Constants.ALL_DIFFICULTIES_PLUS && (Type == DrumsType.FiveLane || Type == DrumsType.ProDrums))
                {
                    break;
                }
            }

            if (Type == DrumsType.UnknownPro)
            {
                Type = DrumsType.ProDrums;
            }
            else if (Type == DrumsType.Unknown)
            {
                Type = DrumsType.FourLane;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using YARG.Data;

namespace YARG.Song.Preparsers
{
    public static class ChartPreparser
    {
        private static readonly Regex ChartEventRegex =
            new Regex(@"(\d+)\s?=\s?[NSE]\s?((\d+\s?\d+)|\w+)", RegexOptions.Compiled);

        private static readonly IReadOnlyDictionary<Difficulty, string> DifficultyLookup =
            new Dictionary<Difficulty, string>
            {
                {
                    Difficulty.EASY, "Easy"
                },
                {
                    Difficulty.MEDIUM, "Medium"
                },
                {
                    Difficulty.HARD, "Hard"
                },
                {
                    Difficulty.EXPERT, "Expert"
                }
            };

        private static readonly IReadOnlyDictionary<Instrument, string> InstrumentLookup =
            new Dictionary<Instrument, string>
            {
                {
                    Instrument.GUITAR, "Single"
                },
                {
                    Instrument.GUITAR_COOP, "DoubleGuitar"
                },
                {
                    Instrument.BASS, "DoubleBass"
                },
                {
                    Instrument.RHYTHM, "DoubleRhythm"
                },
                {
                    Instrument.DRUMS, "Drums"
                },
                {
                    Instrument.KEYS, "Keyboard"
                },
                // { Instrument.GHLiveGuitar, "GHLGuitar" },
                // { Instrument.GHLiveBass, "GHLBass" },
                // { Instrument.GHLiveRhythm, "GHLRhythm" },
                // { Instrument.GHLiveCoop, "GHLCoop" }
            };

        public static bool GetAvailableTracks(byte[] chartData, out ulong tracks)
        {
            using var stream = new MemoryStream(chartData);

            using var reader = new StreamReader(stream);
            try
            {
                tracks = ReadStream(reader);
                return true;
            }
            catch
            {
                tracks = 0;
                return false;
            }
        }

        public static bool GetAvailableTracks(SongEntry song, out ulong tracks)
        {
            using var reader = File.OpenText(Path.Combine(song.Location, song.NotesFile));

            try
            {
                tracks = ReadStream(reader);
                return true;
            }
            catch
            {
                tracks = 0;
                return false;
            }
        }

        private static ulong ReadStream(StreamReader reader)
        {
            ulong tracks = 0;

            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine()?.Trim();
                if (line is null)
                {
                    continue;
                }

                if (line.Length <= 0) continue;

                if (line[0] != '[' && line[^1] != ']') continue;

                string headerName = line[1..^1];
                if (reader.ReadLine()?.Trim() != "{") continue;

                string eventLine = reader.ReadLine()?.Trim();
                if (eventLine is null)
                {
                    continue;
                }

                // This track has an event in it!
                if (!ChartEventRegex.IsMatch(eventLine) || !GetTrackFromHeader(headerName, out var track))
                {
                    continue;
                }

                int shiftAmount = (int) track.instrument * 4 + (int) track.difficulty;
                tracks |= 1UL << shiftAmount;
            }

            return tracks;
        }

        private static bool GetTrackFromHeader(string header, out (Instrument instrument, Difficulty difficulty) track)
        {
            var diffEnums = (Difficulty[]) Enum.GetValues(typeof(Difficulty));
            var instrumentEnums = (Instrument[]) Enum.GetValues(typeof(Instrument));

            foreach (var instrument in instrumentEnums)
            {
                if (!InstrumentLookup.ContainsKey(instrument)) continue;

                foreach (var difficulty in diffEnums)
                {
                    if (!DifficultyLookup.ContainsKey(difficulty)) continue;

                    var trackName = $"{DifficultyLookup[difficulty]}{InstrumentLookup[instrument]}";

                    if (header != trackName)
                    {
                        continue;
                    }

                    track = (instrument, difficulty);
                    return true;
                }
            }

            track = (Instrument.INVALID, (Difficulty) (-1));
            return false;
        }
    }
}
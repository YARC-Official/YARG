using System;
using System.Collections.Generic;
using YARG.Gameplay;

namespace YARG.Data
{
    public sealed class YargChart
    {
        private List<List<NoteInfo>[]> allParts;

        public List<List<NoteInfo>[]> AllParts
        {
            get =>
                allParts ??= new()
                {
                    Guitar,
                    GuitarCoop,
                    Rhythm,
                    Bass,
                    Keys,
                    RealGuitar,
                    RealBass,
                    Drums,
                    RealDrums,
                    GhDrums
                };
            set => allParts = value;
        }

        public List<NoteInfo>[] Guitar = CreateArray();
        public List<NoteInfo>[] GuitarCoop = CreateArray();
        public List<NoteInfo>[] Rhythm = CreateArray();
        public List<NoteInfo>[] Bass = CreateArray();
        public List<NoteInfo>[] Keys = CreateArray();

        public List<NoteInfo>[] RealGuitar = CreateArray();
        public List<NoteInfo>[] RealBass = CreateArray();

        public List<NoteInfo>[] Drums = CreateArray(5);
        public List<NoteInfo>[] RealDrums = CreateArray(5);
        public List<NoteInfo>[] GhDrums = CreateArray(5);

        public List<EventInfo> events = new();
        public List<Beat> beats = new();

        /// <summary>
        /// Lyrics to be displayed in the lyric view when no one is singing.
        /// </summary>
        public List<GenericLyricInfo> genericLyrics = new();

        /// <summary>
        /// Solo vocal lyrics.
        /// </summary>
        public List<LyricInfo> realLyrics = new();

        /// <summary>
        /// Harmony lyrics. Size 0 by default, should be set by the harmony lyric parser.
        /// </summary>
        public List<LyricInfo>[] harmLyrics = new List<LyricInfo>[0];

        public List<NoteInfo>[] GetChartByName(string name)
        {
            return name switch
            {
                "guitar"     => Guitar,
                "guitarCoop" => GuitarCoop,
                "rhythm"     => Rhythm,
                "bass"       => Bass,
                "keys"       => Keys,
                "realGuitar" => RealGuitar,
                "realBass"   => RealBass,
                "drums"      => Drums,
                "realDrums"  => RealDrums,
                "ghDrums"    => GhDrums,
                _            => throw new InvalidOperationException($"Unsupported chart type `{name}`.")
            };
        }

        private static List<NoteInfo>[] CreateArray(int length = 4)
        {
            var list = new List<NoteInfo>[length];
            for (int i = 0; i < length; i++)
            {
                list[i] = new();
            }

            return list;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YARG.Core.Extensions;
using YARG.Core.IO.Ini;

namespace YARG.Core.IO
{
    public enum ChartEventType
    {
        Bpm,
        Time_Sig,
        Anchor,
        Text,
        Note,
        Special,
        Unknown = 255,
    }

    public enum NoteTracks_Chart
    {
        Single,
        DoubleGuitar,
        DoubleBass,
        DoubleRhythm,
        Drums,
        Keys,
        GHLGuitar,
        GHLBass,
        GHLRhythm,
        GHLCoop,
        Invalid,
    };

    public struct DotChartEvent
    {
        public long Position;
        public ChartEventType Type;
    }

    public static class YARGChartFileReader
    {
        public const string HEADERTRACK = "[Song]";
        public const string SYNCTRACK = "[SyncTrack]";
        public const string EVENTTRACK = "[Events]";

        internal static readonly (string name, Difficulty difficulty)[] DIFFICULTIES =
        {
            ("[Easy", Difficulty.Easy),
            ("[Medium", Difficulty.Medium),
            ("[Hard", Difficulty.Hard),
            ("[Expert", Difficulty.Expert),
        };

        internal static readonly (string, Instrument)[] NOTETRACKS =
        {
            new("Single]",       Instrument.FiveFretGuitar),
            new("DoubleGuitar]", Instrument.FiveFretCoopGuitar),
            new("DoubleBass]",   Instrument.FiveFretBass),
            new("DoubleRhythm]", Instrument.FiveFretRhythm),
            new("Drums]",        Instrument.FourLaneDrums),
            new("Keyboard]",     Instrument.Keys),
            new("GHLGuitar]",    Instrument.SixFretGuitar),
            new("GHLBass]",      Instrument.SixFretBass),
            new("GHLRhythm]",    Instrument.SixFretRhythm),
            new("GHLCoop]",      Instrument.SixFretCoopGuitar),
        };

        internal static readonly (string Descriptor, ChartEventType Type)[] EVENTS =
        {
            new("B",  ChartEventType.Bpm),
            new("TS", ChartEventType.Time_Sig),
            new("A",  ChartEventType.Anchor),
            new("E",  ChartEventType.Text),
            new("N",  ChartEventType.Note),
            new("S",  ChartEventType.Special),
        };

        public static bool IsStartOfTrack<TChar>(in YARGTextContainer<TChar> container)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            return !container.IsAtEnd() && container.IsCurrentCharacter('[');
        }

        public static bool ValidateTrack<TChar>(ref YARGTextContainer<TChar> container, string track)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (!DoesStringMatch(ref container, track))
                return false;

            YARGTextReader.GotoNextLine(ref container);
            return true;
        }

        public static bool ValidateInstrument<TChar>(ref YARGTextContainer<TChar> container, out Instrument instrument, out Difficulty difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (ValidateDifficulty(ref container, out difficulty))
            {
                foreach (var (name, inst) in YARGChartFileReader.NOTETRACKS)
                {
                    if (ValidateTrack(ref container, name))
                    {
                        instrument = inst;
                        return true;
                    }
                }
            }
            instrument = default;
            return false;
        }

        private static unsafe bool ValidateDifficulty<TChar>(ref YARGTextContainer<TChar> container, out Difficulty difficulty)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            for (int diffIndex = 3; diffIndex >= 0; --diffIndex)
            {
                var (name, diff) = YARGChartFileReader.DIFFICULTIES[diffIndex];
                if (DoesStringMatch(ref container, name))
                {
                    difficulty = diff;
                    container.Position += name.Length;
                    return true;
                }
            }
            difficulty = default;
            return false;
        }

        private static unsafe bool DoesStringMatch<TChar>(ref YARGTextContainer<TChar> container, string str)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (container.End - container.Position < str.Length)
            {
                return false;
            }

            int index = 0;
            while (index < str.Length && container.Position[index].ToInt32(null) == str[index])
            {
                ++index;
            }
            return index == str.Length;
        }

        public static bool IsStillCurrentTrack<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            YARGTextReader.GotoNextLine(ref container);
            if (container.IsAtEnd())
            {
                return false;
            }

            if (container.IsCurrentCharacter('}'))
            {
                YARGTextReader.GotoNextLine(ref container);
                return false;
            }
            return true;
        }

        public static unsafe bool TryParseEvent<TChar>(ref YARGTextContainer<TChar> container, ref DotChartEvent ev)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (!IsStillCurrentTrack(ref container))
            {
                return false;
            }

            if (!YARGTextReader.TryExtractInt64(ref container, out long position))
            {
                throw new Exception("Could not parse event position");
            }

            if (position < ev.Position)
            {
                throw new Exception($".chart position out of order (previous: {ev.Position})");
            }
            ev.Position = position;
            YARGTextReader.SkipWhitespaceAndEquals(ref container);

            var start = container.Position;
            while (container.Position < container.End)
            {
                int c = container.Position->ToInt32(null) | CharacterExtensions.ASCII_LOWERCASE_FLAG;
                if (c < 'a' || 'z' < c)
                {
                    break;
                }
                ++container.Position;
            }

            long length = container.Position - start;
            ev.Type = ChartEventType.Unknown;
            foreach (var combo in EVENTS)
            {
                if (length != combo.Descriptor.Length)
                {
                    continue;
                }

                int index = 0;
                while (index < length && start[index].ToInt32(null) == combo.Descriptor[index])
                {
                    ++index;
                }

                if (index == length)
                {
                    YARGTextReader.SkipWhitespace(ref container);
                    ev.Type = combo.Type;
                    break;
                }
            }
            return true;
        }

        public static readonly Dictionary<string, IniModifierCreator> CHART_MODIFIERS = new()
        {
            { "Album",        new("album", ModifierType.SortString_Chart) },
            { "Artist",       new("artist", ModifierType.SortString_Chart) },
            { "Charter",      new("charter", ModifierType.SortString_Chart) },
            { "Difficulty",   new("diff_band", ModifierType.Int32) },
            { "Genre",        new("genre", ModifierType.SortString_Chart) },
            { "Name",         new("name", ModifierType.SortString_Chart) },
            { "Offset",       new("delay_chart", ModifierType.Double) },
            { "PreviewEnd",   new("previewEnd_chart", ModifierType.Double) },
            { "PreviewStart", new("previewStart_chart", ModifierType.Double) },
            { "Resolution",   new("Resolution", ModifierType.Int64) },
            { "Year",         new("year_chart", ModifierType.String_Chart) },
        };

        public unsafe static Dictionary<string, List<IniModifier>> ExtractModifiers<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            Dictionary<string, List<IniModifier>> modifiers = new();
            while (IsStillCurrentTrack(ref container))
            {
                string name = YARGTextReader.ExtractModifierName(ref container);
                if (CHART_MODIFIERS.TryGetValue(name, out var node))
                {
                    var mod = node.CreateModifier(ref container);
                    if (modifiers.TryGetValue(node.OutputName, out var list))
                        list.Add(mod);
                    else
                        modifiers.Add(node.OutputName, new() { mod });
                }
            }
            return modifiers;
        }
    }
}
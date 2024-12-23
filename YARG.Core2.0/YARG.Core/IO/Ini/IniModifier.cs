using System;
using System.Text;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace YARG.Core.IO.Ini
{
    public enum ModifierType
    {
        None,
        SortString,
        String,
        SortString_Chart,
        String_Chart,
        UInt64,
        Int64,
        UInt32,
        Int32,
        UInt16,
        Int16,
        Bool,
        Float,
        Double,
        Int64Array,
    };

    public struct IniModifier
    {
        public static readonly IniModifier Default = new()
        {
            SortStr = SortString.Empty,
            Str = string.Empty,
        };

        public unsafe fixed long Buffer[2];
        public SortString SortStr;
        public string Str;
    }

    public readonly struct IniModifierCreator
    {
        public readonly string OutputName;
        public readonly ModifierType Type;

        public IniModifierCreator(string outputName, ModifierType type)
        {
            OutputName = outputName;
            Type = type;
        }

        public unsafe IniModifier CreateModifier<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (Type <= ModifierType.String_Chart)
            {
                var modifier = IniModifier.Default;
                switch (Type)
                {
                    case ModifierType.SortString:
                        modifier.SortStr = SortString.Convert(ExtractIniString(ref container, false));
                        break;
                    case ModifierType.SortString_Chart:
                        modifier.SortStr = SortString.Convert(ExtractIniString(ref container, true));
                        break;
                    case ModifierType.String:
                        modifier.Str = ExtractIniString(ref container, false);
                        break;
                    case ModifierType.String_Chart:
                        modifier.Str = ExtractIniString(ref container, true);
                        break;
                }
                return modifier;
            }
            return CreateNumberModifier(ref container);
        }

        public IniModifier CreateSngModifier(ref YARGTextContainer<byte> sngContainer, int length)
        {
            if (Type <= ModifierType.String)
            {
                var modifier = IniModifier.Default;
                switch (Type)
                {
                    case ModifierType.SortString:
                        modifier.SortStr = SortString.Convert(ExtractSngString(in sngContainer, length));
                        break;
                    case ModifierType.String:
                        modifier.Str = ExtractSngString(in sngContainer, length);
                        break;
                }
                return modifier;
            }
            return CreateNumberModifier(ref sngContainer);
        }

        private unsafe IniModifier CreateNumberModifier<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            var modifier = IniModifier.Default;
            switch (Type)
            {
                case ModifierType.UInt64:
                    YARGTextReader.TryExtractUInt64(ref container, out *(ulong*) modifier.Buffer);
                    break;
                case ModifierType.Int64:
                    YARGTextReader.TryExtractInt64(ref container, out modifier.Buffer[0]);
                    break;
                case ModifierType.UInt32:
                    YARGTextReader.TryExtractUInt32(ref container, out *(uint*)modifier.Buffer);
                    break;
                case ModifierType.Int32:
                    YARGTextReader.TryExtractInt32(ref container, out *(int*)modifier.Buffer);
                    break;
                case ModifierType.UInt16:
                    YARGTextReader.TryExtractUInt16(ref container, out *(ushort*)modifier.Buffer);
                    break;
                case ModifierType.Int16:
                    YARGTextReader.TryExtractInt16(ref container, out *(short*)modifier.Buffer);
                    break;
                case ModifierType.Bool:
                    *(bool*) modifier.Buffer = YARGTextReader.ExtractBoolean(in container);
                    break;
                case ModifierType.Float:
                    YARGTextReader.TryExtractFloat(ref container, out *(float*)modifier.Buffer);
                    break;
                case ModifierType.Double:
                    YARGTextReader.TryExtractDouble(ref container, out *(double*)modifier.Buffer);
                    break;
                case ModifierType.Int64Array:
                    modifier.Buffer[0] = -1;
                    if (YARGTextReader.TryExtractInt64(ref container, out modifier.Buffer[0]))
                    {
                        YARGTextReader.SkipWhitespace(ref container);
                        if (!YARGTextReader.TryExtractInt64(ref container, out modifier.Buffer[1]))
                        {
                            modifier.Buffer[1] = -1;
                        }
                    }
                    else
                    {
                        modifier.Buffer[1] = -1;
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }
            return modifier;
        }

        private static unsafe string ExtractIniString<TChar>(ref YARGTextContainer<TChar> container, bool isChartFile)
            where TChar : unmanaged, IConvertible
        {
            return RichTextUtils.ReplaceColorNames(YARGTextReader.ExtractText(ref container, isChartFile));
        }

        private static unsafe string ExtractSngString(in YARGTextContainer<byte> sngContainer, int length)
        {
            return RichTextUtils.ReplaceColorNames(Encoding.UTF8.GetString(sngContainer.Position, length));
        }
    }
}

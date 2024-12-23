using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Logging;

namespace YARG.Core.IO.Ini
{
    public static class YARGIniReader
    {
        public static Dictionary<string, IniSection> ReadIniFile(string iniPath, Dictionary<string, Dictionary<string, IniModifierCreator>> sections)
        {
            try
            {
                using var bytes = FixedArray<byte>.Load(iniPath);
                if (YARGTextReader.IsUTF8(in bytes, out var byteContainer))
                {
                    return ProcessIni(ref byteContainer, sections);
                }

                using var chars = YARGTextReader.ConvertToUTF16(in bytes, out var charContainer);
                if (chars.IsAllocated)
                {
                    return ProcessIni(ref charContainer, sections);
                }

                using var ints = YARGTextReader.ConvertToUTF32(in bytes, out var intContainer);
                return ProcessIni(ref intContainer, sections);

            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, ex.Message);
                return new();
            }
        }

        private static Dictionary<string, IniSection> ProcessIni<TChar>(ref YARGTextContainer<TChar> container, Dictionary<string, Dictionary<string, IniModifierCreator>> sections)
            where TChar : unmanaged, IConvertible, IEquatable<TChar>
        {
            Dictionary<string, IniSection> modifierMap = new();
            while (TrySection(ref container, out string section))
            {
                if (sections.TryGetValue(section, out var nodes))
                    modifierMap[section] = ExtractModifiers(ref container, ref nodes);
                else
                    YARGTextReader.SkipLinesUntil(ref container, TextConstants<TChar>.OPEN_BRACKET);
            }
            return modifierMap;
        }

        private static bool TrySection<TChar>(ref YARGTextContainer<TChar> container, out string section)
            where TChar : unmanaged, IConvertible, IEquatable<TChar>
        {
            section = string.Empty;
            if (container.IsAtEnd())
            {
                return false;
            }

            if (!container.IsCurrentCharacter('['))
            {
                if (!YARGTextReader.SkipLinesUntil(ref container, TextConstants<TChar>.OPEN_BRACKET))
                {
                    return false;
                }
            }
            section = YARGTextReader.PeekLine(ref container).ToLower();
            return true;
        }

        private static IniSection ExtractModifiers<TChar>(ref YARGTextContainer<TChar> container, ref Dictionary<string, IniModifierCreator> validNodes)
            where TChar : unmanaged, IConvertible, IEquatable<TChar>
        {
            Dictionary<string, List<IniModifier>> modifiers = new();
            while (IsStillCurrentSection(ref container))
            {
                string name = YARGTextReader.ExtractModifierName(ref container).ToLower();
                if (validNodes.TryGetValue(name, out var node))
                {
                    var mod = node.CreateModifier(ref container);
                    if (modifiers.TryGetValue(node.OutputName, out var list))
                        list.Add(mod);
                    else
                        modifiers.Add(node.OutputName, new() { mod });
                }
            }
            return new IniSection(modifiers);
        }

        private static bool IsStillCurrentSection<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible, IEquatable<TChar>
        {
            YARGTextReader.GotoNextLine(ref container);
            return !container.IsAtEnd() && !container.IsCurrentCharacter('[');
        }
    }
}

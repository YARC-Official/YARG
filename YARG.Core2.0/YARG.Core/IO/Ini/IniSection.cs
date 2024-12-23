using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Song;

namespace YARG.Core.IO.Ini
{
    public sealed class IniSection
    {
        private readonly Dictionary<string, List<IniModifier>> modifiers;

        public int Count => modifiers.Count;

        public IniSection() { modifiers = new(); }
        public IniSection(in Dictionary<string, List<IniModifier>> modifiers)
        {
            this.modifiers = modifiers;
        }

        public void Append(in Dictionary<string, List<IniModifier>> modsToAdd)
        {
            foreach (var node in modsToAdd)
            {
                if (modifiers.TryGetValue(node.Key, out var list))
                    list.AddRange(node.Value);
                else
                    modifiers.Add(node.Key, node.Value);
            }
        }

        public bool Contains(in string key)
        {
            return modifiers.ContainsKey(key);
        }

        public bool TryGet(in string key, out SortString str, in SortString defaultStr)
        {
#if DEBUG
            SongIniHandler.ThrowIfNot<SortString>(key);
#endif
            if (modifiers.TryGetValue(key, out var results))
            {
                for (int i = 0; i < results.Count; ++i)
                {
                    if (results[i].SortStr != SortString.Empty)
                    {
                        str = results[i].SortStr;
                        if (str.Str != defaultStr.Str)
                        {
                            return true;
                        }
                    }
                }
            }
            str = defaultStr;
            return false;
        }

        public bool TryGet(in string key, out SortString str, in string defaultStr)
        {
#if DEBUG
            SongIniHandler.ThrowIfNot<SortString>(key);
#endif
            if (modifiers.TryGetValue(key, out var results))
            {
                for (int i = 0; i < results.Count; ++i)
                {
                    if (results[i].SortStr != SortString.Empty)
                    {
                        str = results[i].SortStr;
                        if (str.Str != defaultStr)
                        {
                            return true;
                        }
                    }
                }
            }
            str = defaultStr;
            return false;
        }

        public bool TryGet(in string key, out string str)
        {
#if DEBUG
            SongIniHandler.ThrowIfNot<string>(key);
#endif
            if (modifiers.TryGetValue(key, out var results))
            {
                str = results[0].Str;
                return true;
            }
            str = string.Empty;
            return false;
        }

        public bool TryGet(in string key, out long val1, out long val2)
        {
#if DEBUG
            SongIniHandler.ThrowIfNot<long[]>(key);
#endif
            if (modifiers.TryGetValue(key, out var results))
            {
                unsafe
                {
                    var mod = results[0];
                    val1 = mod.Buffer[0];
                    val2 = mod.Buffer[1];
                }
                return true;
            }
            val1 = -1;
            val2 = -1;
            return false;
        }

        public bool TryGet<T>(in string key, out T val)
            where T : unmanaged
        {
#if DEBUG
            SongIniHandler.ThrowIfNot<T>(key);
#endif
            if (modifiers.TryGetValue(key, out var results))
            {
                unsafe
                {
                    var mod = results[0];
                    val = *(T*) mod.Buffer;
                }
                return true;
            }
            val = default;
            return false;
        }
    }
}

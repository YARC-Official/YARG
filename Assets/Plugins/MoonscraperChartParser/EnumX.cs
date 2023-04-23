// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections;
using System.Linq.Expressions;

namespace MoonscraperEngine
{
    public static class EnumX<EnumType> where EnumType : Enum
    {
        public static readonly EnumType[] Values = (EnumType[])Enum.GetValues(typeof(EnumType));
        public static int Count => Values.Length;
        static readonly string[] StringValues = new string[Count];

        static EnumX()
        {
            for (int i = 0; i < Values.Length; ++i)
            {
                StringValues[i] = Values[i].ToString();
            }
        }

        public static int ToInt(EnumType t)
        {
            return CastTo<int>.From(t);
        }

        public static EnumType FromInt(int t)
        {
            return CastTo<EnumType>.From(t);
        }

        public static bool GenericTryParse(string str, out EnumType enumType)
        {
            for (int i = 0; i < Count; ++i)
            {
                if (string.Equals(str, StringValues[i]))
                {
                    enumType = Values[i];
                    return true;
                }
            }

            enumType = Values[0];

            return false;
        }

        // https://stackoverflow.com/questions/1189144/c-sharp-non-boxing-conversion-of-generic-enum-to-int
        static class CastTo<T>
        {
            /// <summary>
            /// Casts <see cref="S"/> to <see cref="T"/>.
            /// This does not cause boxing for value types.
            /// Useful in generic methods.
            /// </summary>
            /// <typeparam name="S">Source type to cast from. Usually a generic type.</typeparam>
            public static T From<S>(S s)
            {
                return Cache<S>.caster(s);
            }

            private static class Cache<S>
            {
                public static readonly Func<S, T> caster = Get();

                private static Func<S, T> Get()
                {
                    var p = Expression.Parameter(typeof(S));
                    var c = Expression.ConvertChecked(p, typeof(T));
                    return Expression.Lambda<Func<S, T>>(c, p).Compile();
                }
            }
        }
    }
}

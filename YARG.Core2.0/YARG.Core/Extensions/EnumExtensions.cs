using System;
using System.Runtime.CompilerServices;

namespace YARG.Core.Extensions
{
    public static class EnumExtensions<TEnum>
        where TEnum : Enum
    {
        public static readonly TEnum[] Values = (TEnum[])Enum.GetValues(typeof(TEnum));
        public static int Count => Values.Length;
    }

    public static class EnumExtensions
    {
        #region Non-boxing generic conversion of enums: https://stackoverflow.com/a/4026609
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryConvert<TEnum>(this TEnum @enum, out int value)
            where TEnum : unmanaged, Enum
        {
            bool success;
            (value, success) = Unsafe.SizeOf<TEnum>() switch
            {
                sizeof(int)   => (Unsafe.As<TEnum, int>(ref @enum), true),
                sizeof(short) => (Unsafe.As<TEnum, short>(ref @enum), true),
                sizeof(byte)  => (Unsafe.As<TEnum, byte>(ref @enum), true),
                _ => (0, false)
            };

            return success;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Convert<TEnum>(this TEnum @enum)
            where TEnum : unmanaged, Enum
        {
            if (!TryConvert(@enum, out int value))
                throw new ArgumentException($"Cannot convert {typeof(TEnum).Name} to an integer!", nameof(TEnum));

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryConvert<TEnum>(this int value, out TEnum @enum)
            where TEnum : unmanaged, Enum
        {
            short shortValue;
            byte byteValue;
            unchecked
            {
                shortValue = (short) value;
                byteValue = (byte) value;
            }
            bool success;
            (@enum, success) = Unsafe.SizeOf<TEnum>() switch
            {
                sizeof(int)   => (Unsafe.As<int, TEnum>(ref value), true),
                sizeof(short) => (Unsafe.As<short, TEnum>(ref shortValue), true),
                sizeof(byte)  => (Unsafe.As<byte, TEnum>(ref byteValue), true),
                _ => (default, false)
            };

            return success;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TEnum Convert<TEnum>(this int value)
            where TEnum : unmanaged, Enum
        {
            if (!TryConvert(value, out TEnum @enum))
                throw new ArgumentException($"Cannot convert the given integer to {typeof(TEnum).Name}!", nameof(TEnum));

            return @enum;
        }
        #endregion
    }
}
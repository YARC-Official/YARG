using System;
using Cysharp.Text;
using UnityEngine.InputSystem.Utilities;

namespace YARG.Logging
{
    public static partial class LogHandler
    {
        private static void RegisterFormatters()
        {
            Utf16ValueStringBuilder.RegisterTryFormat<FourCC>(TryFormatFourCC);
        }

        private static bool TryFormatFourCC(
            FourCC value,
            Span<char> destination, out int charsWritten,
            ReadOnlySpan<char> format
        )
        {
            charsWritten = 4;

            if (destination.Length < 4)
            {
                return false;
            }

            int code = value;
            destination[0] = (char)((code >> 24) & 0xFF);
            destination[1] = (char)((code >> 16) & 0xFF);
            destination[2] = (char)((code >> 8) & 0xFF);
            destination[3] = (char)((code >> 0) & 0xFF);
            return true;
        }
    }
}
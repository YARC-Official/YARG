using System;

namespace YARG.Core.Engine.ProKeys
{
    public static class ProKeysUtilities
    {
        /*

         One octave of piano key indices for reference (in hex):
          ________________________________
         |  |#| |#|  |  |#| |#| |#|  |   |
         |  |1| |3|  |  |6| |8| |A|  |   |
         | 0 | 2 | 4 | 5 | 7 | 9 | B | 0 |
         |___|___|___|___|___|___|___|___|
         ^----------^ ^-------------^
          Lower Half    Upper Half

        */

        /// <returns>
        /// Whether or not the specified note index is a black key.
        /// </returns>
        /// <param name="noteIndex">The note index of the key mod 12.</param>
        public static bool IsBlackKey(int noteIndex)
        {
            return noteIndex is 1 or 3 or 6 or 8 or 10;
        }

        /// <returns>
        /// Whether or not the specified note index is a white key.
        /// </returns>
        /// <param name="noteIndex">The note index of the key mod 12.</param>
        public static bool IsWhiteKey(int noteIndex)
        {
            return !IsBlackKey(noteIndex);
        }

        /// <return>
        /// <c>true</c> if there is a missing black key (gap) between the specified black key
        /// and the next one.
        /// </return>
        /// <param name="noteIndex">The note index of the key mod 12.</param>
        public static bool IsGapOnNextBlackKey(int noteIndex)
        {
            return noteIndex is 3 or 10;
        }

        /// <return>
        /// <c>true</c> if the specified key is on the lower half of the octave.
        /// </return>
        /// <param name="noteIndex">The note index of the key mod 12.</param>
        public static bool IsLowerHalfKey(int noteIndex)
        {
            return noteIndex is >= 0 and <= 4;
        }

        /// <return>
        /// <c>true</c> if the specified key is on the upper half of the octave.
        /// </return>
        /// <param name="noteIndex">The note index of the key mod 12.</param>
        public static bool IsUpperHalfKey(int noteIndex)
        {
            return !IsLowerHalfKey(noteIndex);
        }

        public static bool IsAdjacentKey(int noteIndex, int adjacentNoteIndex)
        {
            var difference = Math.Abs(adjacentNoteIndex - noteIndex);

            if (difference == 1)
            {
                return true;
            }

            if (IsWhiteKey(noteIndex))
            {
                if (IsWhiteKey(adjacentNoteIndex) && difference == 2)
                {
                    return true;
                }
            } else if (IsBlackKey(noteIndex))
            {
                if (IsBlackKey(adjacentNoteIndex) && difference == 2)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
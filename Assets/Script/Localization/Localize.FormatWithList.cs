using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Localization
{
    public static partial class Localize
    {
        public static string KeyFormat(
            string key,
            IReadOnlyList<string> parameters)
        {
            return parameters.Count switch
            {
                0 => Key(key),

                1 => KeyFormat(
                    key,
                    parameters[0]),

                2 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1]),

                3 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2]),

                4 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3]),

                5 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4]),

                6 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5]),

                7 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6]),

                8 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6],
                    parameters[7]),

                9 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6],
                    parameters[7],
                    parameters[8]),

                10 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6],
                    parameters[7],
                    parameters[8],
                    parameters[9]),

                _ => throw new ArgumentException(
                    "Localization supports a maximum of 10 format parameters.",
                    nameof(parameters))
            };
        }

        public static string KeyFormat<K1, K2>(
            (K1 k1, K2 k2) key,
            IReadOnlyList<string> parameters)
        {
            return parameters.Count switch
            {
                0 => Key(key.k1, key.k2),

                1 => KeyFormat(
                    key,
                    parameters[0]),

                2 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1]),

                3 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2]),

                4 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3]),

                5 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4]),

                6 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5]),

                7 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6]),

                8 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6],
                    parameters[7]),

                9 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6],
                    parameters[7],
                    parameters[8]),

                10 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6],
                    parameters[7],
                    parameters[8],
                    parameters[9]),

                _ => throw new ArgumentException(
                    "Localization supports a maximum of 10 format parameters.",
                    nameof(parameters))
            };
        }

        public static string KeyFormat<K1, K2, K3>(
            (K1 k1, K2 k2, K3 k3) key,
            IReadOnlyList<string> parameters)
        {
            return parameters.Count switch
            {
                0 => Key(key.k1, key.k2, key.k3),

                1 => KeyFormat(
                    key,
                    parameters[0]),

                2 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1]),

                3 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2]),

                4 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3]),

                5 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4]),

                6 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5]),

                7 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6]),

                8 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6],
                    parameters[7]),

                9 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6],
                    parameters[7],
                    parameters[8]),

                10 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6],
                    parameters[7],
                    parameters[8],
                    parameters[9]),

                _ => throw new ArgumentException(
                    "Localization supports a maximum of 10 format parameters.",
                    nameof(parameters))
            };
        }

        public static string KeyFormat<K1, K2, K3, K4>(
            (K1 k1, K2 k2, K3 k3, K4 k4) key,
            IReadOnlyList<string> parameters)
        {
            return parameters.Count switch
            {
                0 => Key(key.k1, key.k2, key.k3, key.k4),

                1 => KeyFormat(
                    key,
                    parameters[0]),

                2 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1]),

                3 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2]),

                4 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3]),

                5 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4]),

                6 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5]),

                7 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6]),

                8 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6],
                    parameters[7]),

                9 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6],
                    parameters[7],
                    parameters[8]),

                10 => KeyFormat(
                    key,
                    parameters[0],
                    parameters[1],
                    parameters[2],
                    parameters[3],
                    parameters[4],
                    parameters[5],
                    parameters[6],
                    parameters[7],
                    parameters[8],
                    parameters[9]),

                _ => throw new ArgumentException(
                    "Localization supports a maximum of 10 format parameters.",
                    nameof(parameters))
            };
        }
    }
}

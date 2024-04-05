using System;
using System.Collections.Generic;

namespace YARG.Helpers.Extensions
{
    public static class ListExtensions
    {
        private static readonly Random Random = new();
        // Thanks! https://stackoverflow.com/questions/273313/randomize-a-listt
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            lock (Random)
            {
                while (n > 1)
                {
                    n--;
                    int k = Random.Next(0, n + 1);
                    (list[k], list[n]) = (list[n], list[k]);
                }
            }
        }

        public static T Pick<T>(this IList<T> list)
        {
            lock (Random)
            {
                return list[Random.Next(0, list.Count)];
            }
        }
    }
}
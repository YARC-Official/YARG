using System.Collections.Generic;
using UnityEngine;

namespace YARG.Helpers.Extensions
{
    public static class ListExtensions
    {
        // Thanks! https://stackoverflow.com/questions/273313/randomize-a-listt
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Range(0, n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        public static T Pick<T>(this IList<T> list)
        {
            return list[Random.Range(0, list.Count)];
        }
    }
}
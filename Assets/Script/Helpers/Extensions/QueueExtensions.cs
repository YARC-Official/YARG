using System.Collections.Generic;
using UnityEngine;

namespace YARG.Helpers.Extensions
{
    public static class QueueExtensions
    {
        public static T PeekOrNull<T>(this Queue<T> queue) where T : class
        {
            if (queue.TryPeek(out var o))
            {
                return o;
            }

            return null;
        }

        public static T ReversePeekOrNull<T>(this Queue<T> queue) where T : class
        {
            if (queue.Count <= 0)
            {
                return null;
            }

            return queue.ToArray()[^1];
        }
    }
}
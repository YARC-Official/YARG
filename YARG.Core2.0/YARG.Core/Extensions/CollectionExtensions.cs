using System;
using System.Collections.Generic;

namespace YARG.Core.Extensions
{
    public static class CollectionExtensions
    {
        /// <summary>
        /// Duplicates the list and every element inside it, such that the new list is
        /// entirely independent and shares no references with the original.
        /// </summary>
        public static List<T> Duplicate<T>(this List<T> list)
            where T : ICloneable<T>
        {
            var newlist = new List<T>();

            foreach (var ev in list)
            {
                var newEvent = ev.Clone();
                newlist.Add(newEvent);
            }

            return newlist;
        }

        /// <summary>
        /// Shuffles the list using the Fisher-Yates shuffle algorithm, using the given random number generator.
        /// </summary>
        /// <remarks>
        /// https://stackoverflow.com/questions/273313/randomize-a-listt
        /// </remarks>
        public static void Shuffle<T>(this List<T> list, Random random)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(0, n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        /// <summary>
        /// Picks a random value from the list using the given random number generator.
        /// </summary>
        public static T PickRandom<T>(this List<T> list, Random random)
        {
            return list[random.Next(0, list.Count)];
        }

        /// <summary>
        /// Searches for an item in the list using the given search object and comparer function.
        /// </summary>
        /// <returns>
        /// The item from the list, or default if the list contains no elements.<br/>
        /// If no exact match was found, the item returned is the one that matches the most closely.
        /// </returns>
        public static TItem? BinarySearch<TItem, TSearch>(this List<TItem> list, TSearch searchObject,
            Func<TItem, TSearch, int> comparer)
        {
            int index = list.BinarySearchIndex(searchObject, comparer);
            if (index < 0)
                return default;

            return list[index];
        }

        /// <summary>
        /// Searches for an item in the list using the given search object and comparer function.
        /// </summary>
        /// <returns>
        /// The index of the item in the list, or -1 if the list contains no elements.<br/>
        /// If no exact match was found, the index returned is that of the item that matches the most closely.
        /// </returns>
        public static int BinarySearchIndex<TItem, TSearch>(this List<TItem> list, TSearch searchObject,
            Func<TItem, TSearch, int> comparer)
        {
            int low = 0;
            int high = list.Count - 1;
            int index = -1;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                index = mid;

                var current = list[mid];
                int comparison = comparer(current, searchObject);
                if (comparison == 0)
                {
                    // The objects are equal
                    return index;
                }
                else if (comparison < 0)
                {
                    // The current object is less than the search object, exclude current lower bound
                    low = mid + 1;
                }
                else
                {
                    // The current object is greater than the search object, exclude current higher bound
                    high = mid - 1;
                }
            }

            return index;
        }

        /// <summary>
        /// Attempts to peek at the beginning of the queue.
        /// </summary>
        /// <returns>
        /// The peeked value, if available; otherwise the default value of <typeparamref name="T"/>.
        /// </returns>
        public static T? PeekOrDefault<T>(this Queue<T> queue)
        {
            if (queue.TryPeek(out var o))
            {
                return o;
            }

            return default;
        }

        /// <summary>
        /// Attempts to dequeue a value from the queue.
        /// </summary>
        /// <returns>
        /// The peeked value, if available; otherwise the default value of <typeparamref name="T"/>.
        /// </returns>
        public static T? DequeueOrDefault<T>(this Queue<T> queue)
        {
            if (queue.TryDequeue(out var o))
            {
                return o;
            }

            return default;
        }
    }
}
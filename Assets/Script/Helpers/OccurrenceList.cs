namespace System.Collections.Generic
{
    /// <summary>
    /// Stores the number of times <typeparamref name="T"/> occurs. Occurrences can easily be added and manipulated.
    /// </summary>
    public class OccurrenceList<T> : IEnumerable<KeyValuePair<T, int>>
    {
        /// <summary>
        /// A dictionary that stores the actual occurrences.
        /// </summary>
        private Dictionary<T, int> occurrences = new();

        /// <returns>
        /// Whether or not the list contains any items. Items with a count of zero are not included.
        /// </returns>
        public bool Empty
        {
            get
            {
                foreach (var pair in occurrences)
                {
                    if (pair.Value != 0)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Creates an <see cref="OccurrenceList{T}"/> from the provided <see cref="IEnumerable{T}"/>.
        /// </summary>
        /// <param name="enumerable">The <c><see cref="IEnumerable{T}"/></c> to check the occurrences from.</param>
        public OccurrenceList(IEnumerable<T> enumerable)
        {
            foreach (var item in enumerable)
            {
                Add(item);
            }
        }

        /// <summary>
        /// Adds an occurence or multiple occurences to the list.
        /// </summary>
        /// <param name="item">The key or item that has an occurence.</param>
        /// <param name="count">The number of occurences that should be added.</param>
        public void Add(T item, int count = 1)
        {
            if (occurrences.ContainsKey(item))
            {
                occurrences[item] += count;
            }
            else
            {
                occurrences[item] = count;
            }
        }

        /// <summary>
        /// Removes an occurence or multiple occurences to the list.
        /// </summary>
        /// <param name="item">The key or item that has an occurence.</param>
        /// <param name="count">The number of occurences that should be removed.</param>
        /// <param name="removeIfZero">Whether or not the element should be removed from the list if it the count reaches 0.</param>
        public void Remove(T item, int count = 1, bool removeIfZero = false)
        {
            Add(item, -count);

            if (removeIfZero && occurrences[item] <= 0)
            {
                occurrences.Remove(item);
            }
        }

        /// <summary>
        /// Sets the number of times an that <c>item</c> occurs to zero.<br/>
        /// This does not remove it from the list. See: <see cref="RemoveAllZeros"/>
        /// </summary>
        /// <param name="item">The key or item in which the occurences should be set to zero.</param>
        /// <param name="removeFromList">Whether or not the element should be removed from the list instead of its count being set to 0.</param>
        public void RemoveAll(T item, bool removeFromList = false)
        {
            if (removeFromList)
            {
                occurrences.Remove(item);
            }
            else
            {
                occurrences[item] = 0;
            }
        }

        /// <summary>
        /// Removes all of the items where the number of times it occurs is zero.
        /// </summary>
        public void RemoveAllZeros()
        {
            foreach (var pair in new Dictionary<T, int>(occurrences))
            {
                if (pair.Value == 0)
                {
                    occurrences.Remove(pair.Key);
                }
            }
        }

        /// <returns>
        /// The number of times that <c>item</c> occurs. If <c>item</c> is not in the list, it will return <c>0</c>.
        /// </returns>
        /// <param name="item">The key or item to check.</param>
        public int GetCount(T item)
        {
            return occurrences.ContainsKey(item) ? occurrences[item] : 0;
        }

        /// <returns>
        /// The first element in the list of occurrences.
        /// </returns>
        public KeyValuePair<T, int> GetFirst()
        {
            foreach (var pair in occurrences)
            {
                if (pair.Value != 0)
                {
                    return pair;
                }
            }

            throw new IndexOutOfRangeException("The OccurrenceList is empty.");
        }

        /// <summary>
        /// Clears the occurrences list.
        /// </summary>
        public void Clear()
        {
            occurrences.Clear();
        }

        /// <returns>
        /// The occurence list in dictionary form.
        /// </returns>
        public Dictionary<T, int> ToDictionary()
        {
            return new Dictionary<T, int>(occurrences);
        }

        public IEnumerator<KeyValuePair<T, int>> GetEnumerator()
        {
            return occurrences.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

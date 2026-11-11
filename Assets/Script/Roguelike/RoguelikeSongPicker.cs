using System;
using System.Collections.Generic;

namespace YARG.Roguelike
{
    /// <summary>
    /// Picks the songs offered in a roguelike round. Generic over the song type so it can be tested
    /// without the Unity song library; <see cref="RoguelikeLibrary"/> plugs in the real one.
    /// </summary>
    public static class RoguelikeSongPicker
    {
        /// <summary>
        /// Pick up to <paramref name="count"/> distinct songs, preferring the target intensity tier.
        /// </summary>
        /// <remarks>
        /// If the target tier has too few eligible songs, the rest are drawn from neighbouring tiers,
        /// nearest first and easier before harder at the same distance (target, -1, +1, -2, +2, ...),
        /// staying within [<paramref name="minIntensity"/>, <paramref name="maxIntensity"/>]. Songs
        /// with an intensity outside that range, including -1 (unknown), are never offered. Fewer
        /// than <paramref name="count"/> songs are returned only if the whole range runs dry.
        /// </remarks>
        public static List<TSong> Pick<TSong>(
            IEnumerable<TSong> pool,
            Func<TSong, int> getIntensity,
            Func<TSong, bool> isExcluded,
            int targetIntensity,
            int count,
            int minIntensity,
            int maxIntensity,
            Random random)
        {
            if (pool is null) throw new ArgumentNullException(nameof(pool));
            if (getIntensity is null) throw new ArgumentNullException(nameof(getIntensity));
            if (random is null) throw new ArgumentNullException(nameof(random));

            var picked = new List<TSong>(Math.Max(count, 0));
            if (count <= 0 || maxIntensity < minIntensity)
            {
                return picked;
            }

            targetIntensity = Math.Clamp(targetIntensity, minIntensity, maxIntensity);

            // Bucket eligible songs by tier
            var tiers = new Dictionary<int, List<TSong>>();
            foreach (var song in pool)
            {
                if (isExcluded != null && isExcluded(song))
                {
                    continue;
                }

                int intensity = getIntensity(song);
                if (intensity < minIntensity || intensity > maxIntensity)
                {
                    continue;
                }

                if (!tiers.TryGetValue(intensity, out var bucket))
                {
                    bucket = new List<TSong>();
                    tiers.Add(intensity, bucket);
                }
                bucket.Add(song);
            }

            foreach (int tier in TierSearchOrder(targetIntensity, minIntensity, maxIntensity))
            {
                if (!tiers.TryGetValue(tier, out var bucket))
                {
                    continue;
                }

                Shuffle(bucket, random);
                foreach (var song in bucket)
                {
                    picked.Add(song);
                    if (picked.Count == count)
                    {
                        return picked;
                    }
                }
            }

            return picked;
        }

        /// <summary>Tiers in the order they are searched: target, -1, +1, -2, +2, ... within bounds.</summary>
        public static IEnumerable<int> TierSearchOrder(int target, int min, int max)
        {
            yield return target;
            for (int distance = 1; target - distance >= min || target + distance <= max; distance++)
            {
                if (target - distance >= min) yield return target - distance;
                if (target + distance <= max) yield return target + distance;
            }
        }

        private static void Shuffle<T>(IList<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}

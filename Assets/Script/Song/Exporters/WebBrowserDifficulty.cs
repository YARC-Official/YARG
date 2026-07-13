using System.Collections.Generic;

namespace YARG.Song.Exporters
{
    /// <summary>
    /// Pure difficulty encoder for the web song browser export.
    /// Uses base-36 encoding: 0-9 for tiers 0-9, a-z for tiers 10-35.
    /// Special states: '.' (absent), '?' (present but unknown tier).
    /// </summary>
    public static class WebBrowserDifficulty
    {
        /// <summary>
        /// Encodes a tier (0-35) as a single base-36 character.
        /// Tiers 0-9 map to '0'-'9', tiers 10-35 map to 'a'-'z'.
        /// </summary>
        /// <param name="tier">The tier value to encode (assumed 0-35).</param>
        /// <returns>A single character representing the tier.</returns>
        /// <remarks>
        /// Real song tiers are single-digit (0-6). Base-36 headroom covers pathological data.
        /// Tiers > 35 are not expected and would encode to non-alphanumeric characters.
        /// </remarks>
        public static char EncodeTier(int tier)
        {
            // Guard: negative tiers should never reach here (callers filter them).
            // If they do, we treat them as unknown rather than corrupting output.
            if (tier < 0)
            {
                return '?';
            }

            // Base-36: 0-9 for tiers 0-9, a-z for tiers 10-35.
            if (tier < 10)
            {
                return (char)('0' + tier);
            }
            else
            {
                return (char)('a' + tier - 10);
            }
        }

        /// <summary>
        /// Encodes an aggregated slot (multiple sub-types) into a single character.
        /// Aggregates by taking the maximum known tier across existing sub-types.
        /// </summary>
        /// <param name="subTypes">List of (exists, intensity) tuples for each sub-type.</param>
        /// <returns>
        /// '.' if no sub-type exists,
        /// '?' if at least one exists but none have a known intensity (all < 0),
        /// otherwise the base-36 character of the maximum known tier.
        /// </returns>
        /// <remarks>
        /// This implements the aggregation logic for parts that combine multiple instruments:
        /// - Vocals: Vocals + Harmony
        /// - Guitar: 5-fret + 6-fret variants
        /// - Drums: 4-lane + Pro + 5-lane
        /// - Keys: Keys + ProKeys
        /// - Bass: 5-fret + 6-fret variants
        /// </remarks>
        public static char EncodeSlot(IReadOnlyList<(bool exists, int intensity)> subTypes)
        {
            // Check if any sub-type exists.
            bool anyExists = false;
            foreach (var (exists, _) in subTypes)
            {
                if (exists)
                {
                    anyExists = true;
                    break;
                }
            }

            // If no sub-type exists, the slot is absent.
            if (!anyExists)
            {
                return '.';
            }

            // Collect intensities of existing sub-types with known tiers (>= 0).
            int maxKnownTier = -1;
            foreach (var (exists, intensity) in subTypes)
            {
                if (exists && intensity >= 0)
                {
                    if (intensity > maxKnownTier)
                    {
                        maxKnownTier = intensity;
                    }
                }
            }

            // If we have at least one known tier, encode the maximum.
            if (maxKnownTier >= 0)
            {
                return EncodeTier(maxKnownTier);
            }

            // At least one sub-type exists, but none have a known tier -> present but unknown.
            return '?';
        }
    }
}

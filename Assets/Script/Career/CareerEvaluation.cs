using System;
using System.Collections.Generic;
using System.Linq;

namespace YARG.Career
{
    // Computed career progress for a single save: best stars per song, tier stars, and unlock state.
    // Evaluated above the database layer from raw rows (CareerProgressSnapshot), never from general
    // score records.
    public class CareerEvaluation
    {
        public class SongResult
        {
            public Guid CareerSongId;
            public int SongIndex;
            public bool Completed;
            public int BestStars;
        }

        public class TierResult
        {
            public Guid TierId;
            public int TierIndex;
            public bool IsBonus;
            public bool Unlocked;
            public bool Completed;
            // Average of the tier's songs' best stars (display value, 0..5 scale)
            public float Stars;
            // Sum of the tier's songs' best stars (used for StarCount unlocks)
            public int StarSum;
            // How much more the previous *regular* tier still needs in order to reach this tier's
            // unlock criteria, measured in this tier's UnlockType metric. 0 once unlocked.
            // Content that cannot be reached at all (no regular tier before it) reports its full
            // criteria here.
            public int RemainingToUnlock;
            public List<SongResult> Songs = new();
        }

        public int CareerSaveId;
        public List<TierResult> Tiers = new();

        public static CareerEvaluation Evaluate(CareerBase career, CareerProgressSnapshot snapshot)
        {
            var evaluation = new CareerEvaluation
            {
                CareerSaveId = snapshot.CareerSaveId,
            };

            // Best stars per song: max BandStars across all committed completions for that song.
            // Replays insert extra completion rows; taking the best means replays can improve a song
            // but never farm progress.
            var bestStarsBySong = new Dictionary<Guid, int>();
            foreach (var completion in snapshot.Completions)
            {
                if (!bestStarsBySong.TryGetValue(completion.CareerSongId, out var currentBest) ||
                    completion.BandStars > currentBest)
                {
                    bestStarsBySong[completion.CareerSongId] = completion.BandStars;
                }
            }

            TierResult previousRegularTier = null;

            for (var tierIndex = 0; tierIndex < career.Tiers.Count; tierIndex++)
            {
                var tier = career.Tiers[tierIndex];
                var result = new TierResult
                {
                    TierId = tier.Id,
                    TierIndex = tierIndex,
                    IsBonus = tier.IsBonus,
                };

                for (var songIndex = 0; songIndex < tier.Songs.Length; songIndex++)
                {
                    var song = tier.Songs[songIndex];
                    result.Songs.Add(new SongResult
                    {
                        CareerSongId = song.Id,
                        SongIndex = songIndex,
                        Completed = bestStarsBySong.ContainsKey(song.Id),
                        BestStars = bestStarsBySong.GetValueOrDefault(song.Id),
                    });
                }

                result.Completed = result.Songs.Count > 0 && result.Songs.All(song => song.Completed);
                result.StarSum = result.Songs.Sum(song => song.BestStars);
                result.Stars = result.Songs.Count == 0 ? 0 : (float) result.StarSum / result.Songs.Count;

                // The first tier is always unlocked. Otherwise, the previous *regular* tier must meet
                // this tier's unlock criteria: bonus tiers are excluded from unlocking other tiers.
                if (tierIndex == 0)
                {
                    result.Unlocked = true;
                }
                else if (previousRegularTier is not null)
                {
                    switch (tier.UnlockType)
                    {
                        case UnlockType.CompletionCount:
                        {
                            var completed = previousRegularTier.Songs.Count(song => song.Completed);
                            result.Unlocked = completed >= tier.UnlockCriteria;
                            result.RemainingToUnlock = Math.Max(0, tier.UnlockCriteria - completed);
                            break;
                        }
                        case UnlockType.StarCount:
                        {
                            result.Unlocked = previousRegularTier.StarSum >= tier.UnlockCriteria;
                            result.RemainingToUnlock =
                                Math.Max(0, tier.UnlockCriteria - previousRegularTier.StarSum);
                            break;
                        }
                    }
                }
                else
                {
                    // Only bonus tiers precede this one, and bonus tiers do not count towards
                    // unlocks, so no progress can reach it. Left locked; the menu reports the full
                    // criteria as still required. Content should not be authored this way.
                    result.RemainingToUnlock = Math.Max(0, tier.UnlockCriteria);
                }

                if (!tier.IsBonus)
                {
                    previousRegularTier = result;
                }

                evaluation.Tiers.Add(result);
            }

            return evaluation;
        }
    }
}

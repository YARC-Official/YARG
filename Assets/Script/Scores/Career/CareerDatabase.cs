using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;
using YARG.Career;

namespace YARG.Scores
{
    public class CareerDatabase
    {
        private readonly SQLiteConnection _db;

        public CareerDatabase(SQLiteConnection db)
        {
            _db = db;
        }

        public void Initialize()
        {
            _db.CreateTable<CareerSaves>();
            _db.CreateTable<CareerSaveProfiles>();
            _db.CreateTable<CareerSongCompletions>();
            _db.CreateTable<CareerTierProgress>();
        }

        #region Reads

        public List<CareerSongCompletions> GetSongCompletions(int careerSaveId)
        {
            return _db.Query<CareerSongCompletions>(@"SELECT * FROM CareerSongCompletions
                                                    WHERE CareerSaveId = ?",
                                                    careerSaveId);
        }

        public List<CareerTierProgress> GetTierProgress(int careerSaveId)
        {
            return _db.Query<CareerTierProgress>(@"SELECT * FROM CareerTierProgress
                                                    WHERE CareerSaveId = ?",
                                                    careerSaveId);
        }

        /// <summary>
        /// Load all raw progress rows for a single save in one go, so progress can be evaluated
        /// above the database layer.
        /// </summary>
        public CareerProgressSnapshot LoadProgressSnapshot(int careerSaveId)
        {
            return new CareerProgressSnapshot
            {
                CareerSaveId = careerSaveId,
                Completions = GetSongCompletions(careerSaveId),
                TierProgress = GetTierProgress(careerSaveId),
            };
        }

        /// <summary>
        /// All active (not soft-deleted) saves for a profile.
        /// </summary>
        public List<CareerSaves> GetActiveSavesForProfile(Guid profileId)
        {
            return _db.Query<CareerSaves>(
                @"SELECT cs.* FROM CareerSaves cs
                  INNER JOIN CareerSaveProfiles csp ON csp.CareerSaveId = cs.Id
                  WHERE csp.ProfileId = ? AND cs.IsDeleted = 0",
                profileId);
        }

        #endregion

        #region Save resolution

        /// <summary>
        /// Find the active save for this career whose member set exactly matches the given profiles.
        /// A save belongs to a *set* of players (one CareerSaveProfiles row per member); solo play is
        /// a one-member set, band play an N-member set. Returns null if no such save exists.
        /// </summary>
        public CareerSaves FindActiveSaveForProfiles(Guid careerId, IEnumerable<Guid> profileIds)
        {
            var expected = new HashSet<Guid>(profileIds);
            if (expected.Count == 0)
            {
                return null;
            }

            var candidates = _db.Query<CareerSaves>(
                @"SELECT DISTINCT cs.* FROM CareerSaves cs
                  INNER JOIN CareerSaveProfiles csp ON csp.CareerSaveId = cs.Id
                  WHERE cs.CareerId = ? AND cs.IsDeleted = 0",
                careerId);

            foreach (var candidate in candidates)
            {
                var members = _db.Query<CareerSaveProfiles>(
                    "SELECT * FROM CareerSaveProfiles WHERE CareerSaveId = ?", candidate.Id)
                                 .Select(p => p.ProfileId)
                                 .ToHashSet();
                if (members.SetEquals(expected))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolve the active save for (career, player set), creating one if none exists.
        /// "New career" soft-deletes the old save so a fresh one is created.
        /// </summary>
        public int GetOrCreateSave(Guid careerId, int careerVersion, IReadOnlyCollection<Guid> profileIds)
        {
            var existing = FindActiveSaveForProfiles(careerId, profileIds);
            if (existing is not null)
            {
                return existing.Id;
            }

            var save = new CareerSaves
            {
                CareerId = careerId,
                CareerVersion = careerVersion,
                CreatedAt = DateTime.Now,
            };
            _db.Insert(save);

            foreach (var profileId in profileIds.Distinct())
            {
                _db.Insert(new CareerSaveProfiles
                {
                    CareerSaveId = save.Id,
                    ProfileId = profileId,
                });
            }

            return save.Id;
        }

        /// <summary>
        /// "New career": soft-delete the save. Progress history is preserved for comparison.
        /// </summary>
        public void SoftDeleteSave(int careerSaveId)
        {
            _db.Execute("UPDATE CareerSaves SET IsDeleted = 1 WHERE Id = ?", careerSaveId);
        }

        #endregion

        #region Commits

        /// <summary>
        /// Commit a single song completion in a transaction.
        /// Replays insert new rows on purpose; star aggregation uses best-per-song so extra rows
        /// never farm progress.
        /// </summary>
        public int CommitSongCompletion(CareerSongCompletionCommit commit)
        {
            var completion = new CareerSongCompletions
            {
                CareerSaveId = commit.CareerSaveId,
                CareerId = commit.CareerId,
                TierId = commit.TierId,
                TierIndex = commit.TierIndex,
                CareerSongId = commit.CareerSongId,
                SongIndex = commit.SongIndex,
                SongChecksum = commit.SongChecksum,
                GameRecordId = commit.GameRecordId,
                BandStars = commit.BandStars,
                BandScore = commit.BandScore,
                CompletedAt = commit.CompletedAt,
                Source = commit.Source,
            };

            _db.RunInTransaction(() =>
            {
                _db.Insert(completion);

                _db.Execute("UPDATE CareerSaves SET LastPlayedAt = ? WHERE Id = ?",
                    DateTime.Now, commit.CareerSaveId);
            });

            return completion.Id;
        }

        #endregion

        #region Tier progress

        /// <summary>
        /// Mark a tier as unlocked (insert the progress row if needed). Idempotent.
        /// </summary>
        public void MarkTierUnlocked(int careerSaveId, Guid tierId, int tierIndex)
        {
            var existing = FindTierProgress(careerSaveId, tierId);

            if (existing is null)
            {
                _db.Insert(new CareerTierProgress
                {
                    CareerSaveId = careerSaveId,
                    TierId = tierId,
                    TierIndex = tierIndex,
                    UnlockedAt = DateTime.Now,
                });
                return;
            }

            if (existing.UnlockedAt.HasValue)
            {
                return;
            }

            existing.UnlockedAt = DateTime.Now;
            _db.Update(existing);
        }

        /// <summary>
        /// Mark a tier as completed. Idempotent.
        /// </summary>
        public void MarkTierCompleted(int careerSaveId, Guid tierId)
        {
            var existing = FindTierProgress(careerSaveId, tierId);

            if (existing is null || existing.CompletedAt.HasValue)
            {
                return;
            }

            existing.CompletedAt = DateTime.Now;
            _db.Update(existing);
        }

        /// <summary>
        /// UI state: the player has seen this tier's unlock presentation.
        /// </summary>
        public void MarkUnlockSeen(int careerSaveId, Guid tierId)
        {
            _db.Execute("UPDATE CareerTierProgress SET UnlockSeen = 1 WHERE CareerSaveId = ? AND TierId = ?",
                careerSaveId, tierId);
        }

        /// <summary>
        /// UI state: the player has seen this tier's completion bonus media.
        /// </summary>
        public void MarkCompletionBonusSeen(int careerSaveId, Guid tierId)
        {
            _db.Execute("UPDATE CareerTierProgress SET CompletionBonusSeen = 1 WHERE CareerSaveId = ? AND TierId = ?",
                careerSaveId, tierId);
        }

        private CareerTierProgress FindTierProgress(int careerSaveId, Guid tierId)
        {
            return _db.FindWithQuery<CareerTierProgress>(
                "SELECT * FROM CareerTierProgress WHERE CareerSaveId = ? AND TierId = ?",
                careerSaveId, tierId);
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using YARG.Core.Game;
using YARG.Scores;

namespace YARG.Career
{
    public partial class CareerBase : BasePreset
    {
        public string Title;
        public string Description;
        public string BackgroundImageName;
        public string Source;
        public int    Version = 1;

        private readonly List<CareerTier>          _tiers;
        public           IReadOnlyList<CareerTier> Tiers => _tiers;

        public CareerBase(Guid id, string title, string description, bool defaultPreset) : base(title, defaultPreset)
        {
            Id = id;
            Title = title;
            Description = description;
        }

        public CareerBase(Guid id, string name, string description, string bgImage, bool defaultPreset, CareerTier[] tiers) : base(name, defaultPreset)
        {
            Id = id;
            Name = name;
            Description = description;
            _tiers = tiers.ToList();

            BackgroundImageName = bgImage;
        }

        [JsonConstructor]
        public CareerBase(Guid id, string name, string description, string bgImage, string source, bool defaultPreset,
            CareerTier[] tiers) : base(name, defaultPreset)
        {
            Id = id;
            Name = name;
            Description = description;
            BackgroundImageName = bgImage;
            Source = source;
            _tiers = tiers.ToList();
        }

        /// <summary>
        /// Fire after a song completion has been committed for this career: re-evaluate progress from
        /// the raw rows and apply tier unlock/completion changes. Scaffolding for now — the trigger
        /// is a direct call from the score path; an event from GameManager can replace it later.
        /// </summary>
        public void SyncProgress(CareerDatabase careers, int careerSaveId)
        {
            if (careers is null || careerSaveId <= 0)
            {
                return;
            }

            var snapshot = careers.LoadProgressSnapshot(careerSaveId);
            var evaluation = CareerEvaluation.Evaluate(this, snapshot);

            foreach (var tier in evaluation.Tiers)
            {
                if (tier.Unlocked)
                {
                    careers.MarkTierUnlocked(careerSaveId, tier.TierId, tier.TierIndex);
                }

                if (tier.Completed)
                {
                    careers.MarkTierCompleted(careerSaveId, tier.TierId);
                }
            }
        }

        private void AddTier(CareerTier tier)
        {
            _tiers.Add(tier);
        }

        public void RefreshSongEntries()
        {
            foreach (var tier in _tiers)
            {
                tier.RefreshSongEntries();
            }
        }

        public override BasePreset CopyWithNewName(string name)
        {
            var tiers = new CareerTier[_tiers.Count];

            for (var i = 0; i < _tiers.Count; i++)
            {
                tiers[i] = new CareerTier(_tiers[i], this);
            }

            return new CareerBase(Guid.NewGuid(), name, Description, BackgroundImageName, Source, DefaultPreset, tiers);
        }

        public override string ToString()
        {
            return $"CareerBase: {Title}, Tiers: {Tiers.Count}";
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using YARG.Core.Game;
using YARG.Core.Game.Settings;
using YARG.Scores;
using YARG.Settings.Customization;

namespace YARG.Career
{
    public partial class CareerPreset : BasePreset
    {
        public string Title;
        [SettingType(SettingType.String)]
        public string Description;
        [SettingType(SettingType.FileInfo)]
        [JsonConverter(typeof(JsonFileInfoConverter))]
        public FileInfo BackgroundImageName;
        [SettingType(SettingType.String)]
        public string Source;
        public int    Version = 1;

        private readonly List<CareerTier>          _tiers;
        public           IReadOnlyList<CareerTier> Tiers => _tiers;

        public CareerPreset(Guid id, string title, string description, bool defaultPreset) : base(title, defaultPreset)
        {
            Id = id;
            Title = title;
            Description = description;
            _tiers = new List<CareerTier>();
        }

        public CareerPreset(Guid id, string name, string description, string bgImage, bool defaultPreset, CareerTier[] tiers) : base(name, defaultPreset)
        {
            Id = id;
            Name = name;
            Description = description;
            _tiers = tiers?.ToList() ?? new List<CareerTier>();

            BackgroundImageName = CreateFileInfo(bgImage);
        }

        [JsonConstructor]
        public CareerPreset(Guid id, string name, string description, string backgroundImageName, string source, bool defaultPreset,
            CareerTier[] tiers) : base(name, defaultPreset)
        {
            Id = id;
            Name = name;
            Description = description;
            BackgroundImageName = CreateFileInfo(backgroundImageName);
            Source = source;
            _tiers = tiers?.ToList() ?? new List<CareerTier>();
        }

        private static FileInfo CreateFileInfo(string filename)
        {
            return string.IsNullOrWhiteSpace(filename) ? null : new FileInfo(System.IO.Path.GetFileName(filename));
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

        public void AddTier(CareerTier tier)
        {
            if (tier == null)
            {
                return;
            }

            _tiers.Add(tier);
        }

        public void InsertTier(int index, CareerTier tier)
        {
            if (tier == null)
            {
                return;
            }

            if (index < 0)
            {
                index = 0;
            }

            if (index > _tiers.Count)
            {
                index = _tiers.Count;
            }

            _tiers.Insert(index, tier);
        }

        public bool RemoveTier(CareerTier tier)
        {
            return _tiers.Remove(tier);
        }

        public void RemoveTierAt(int index)
        {
            if (index >= 0 && index < _tiers.Count)
            {
                _tiers.RemoveAt(index);
            }
        }

        public void MoveTier(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _tiers.Count || toIndex < 0 || toIndex >= _tiers.Count || fromIndex == toIndex)
                return;

            var tier = _tiers[fromIndex];
            _tiers.RemoveAt(fromIndex);
            _tiers.Insert(toIndex, tier);
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
                tiers[i] = new CareerTier(_tiers[i]);
            }

            // TODO: Create extra content folder and copy content from source preset

            return new CareerPreset(Guid.NewGuid(), name, Description, BackgroundImageName?.Name, Source, false, tiers);
        }

        public override string ToString()
        {
            return $"CareerPreset: {Title}, Tiers: {Tiers.Count}";
        }
    }
}
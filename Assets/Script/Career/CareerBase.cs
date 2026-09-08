using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine.UI;
using YARG.Core.Game;
using YARG.Core.IO;

namespace YARG.Career
{
    public class CareerBase : BasePreset
    {
        public Guid   CareerId;
        public string Title;
        public string Description;
        public string BackgroundImageName;
        public int    Version = 1;

        private readonly List<CareerTier>          _tiers;
        public           IReadOnlyList<CareerTier> Tiers => _tiers;

        [NonSerialized]
        public RawImage BackgroundImage;

        public CareerBase(Guid id, string title, string description) : base(title, true)
        {
            Id = id;
            Title = title;
            Description = description;
        }

        [JsonConstructor]
        public CareerBase(Guid id, string name, string description, string bgImage, CareerTier[] tiers) : base(name, true)
        {
            Id = id;
            Name = name;
            Description = description;
            _tiers = tiers.ToList();

            BackgroundImageName = bgImage;
            // Find the background image and load it
            // if (BackgroundImageName != null)
            // {
            //     var image = YARGImage.Load(BackgroundImageName);
            //     BackgroundImage = image.LoadTexture();
            // }
        }

        private void AddTier(CareerTier tier)
        {
            _tiers.Add(tier);
        }

        public override BasePreset CopyWithNewName(string name)
        {
            var tiers = new CareerTier[_tiers.Count];

            for (var i = 0; i < _tiers.Count; i++)
            {
                tiers[i] = new CareerTier(_tiers[i], this);
            }

            return new CareerBase(Guid.NewGuid(), name, Description, BackgroundImageName, tiers);
        }

        public override string ToString()
        {
            return $"CareerBase: {Title}, Tiers: {Tiers.Count}";
        }
    }
}
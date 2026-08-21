using System;

namespace YARG.Career
{
    public class CareerBase
    {
        public Guid Id;
        public string Name;
        public string Description;

        public CareerTier[] Tiers;

        public CareerBase()
        {
            Id = Guid.NewGuid();
        }

        public CareerBase(string name, string description)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
        }

        public CareerBase(Guid id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }

        public CareerBase(Guid id, string name, string description, CareerTier[] tiers)
        {
            Id = id;
            Name = name;
            Description = description;
            Tiers = tiers;
        }

        public override string ToString()
        {
            return $"CareerBase: {Name}, Tiers: {Tiers.Length}";
        }
    }
}
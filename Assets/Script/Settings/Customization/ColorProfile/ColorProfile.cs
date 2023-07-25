using UnityEngine;

namespace YARG.Settings.Customization
{
    public partial class ColorProfile
    {
        public static ColorProfile Default => new("Default");

        public string Name;
        public FiveFretGuitarColors FiveFretGuitar;

        public ColorProfile(string name)
        {
            Name = name;
        }
    }
}
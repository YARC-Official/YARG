namespace YARG.Settings.Customization
{
    // TODO: Move to YARG.Core

    public partial class EnginePreset
    {
        /// <summary>
        /// A preset for a hit window. This should
        /// be used within each engine preset class.
        /// </summary>
        public class HitWindowPreset
        {
            public double MaxWindow;
            public double MinWindow;

            public bool IsDynamic;

            public double FrontToBackRatio = 1.0;

            public HitWindowPreset Copy()
            {
                return new HitWindowPreset
                {
                    MaxWindow = MaxWindow,
                    MinWindow = MinWindow,
                    IsDynamic = IsDynamic,
                    FrontToBackRatio = FrontToBackRatio,
                };
            }
        }

        /// <summary>
        /// The engine preset for five fret guitar.
        /// </summary>
        public class FiveFretGuitarPreset
        {
            public bool AntiGhosting     = true;
            public bool InfiniteFrontEnd = false;

            public double HopoLeniency = 0.08;

            public double StrumLeniency      = 0.06;
            public double StrumLeniencySmall = 0.025;

            public HitWindowPreset HitWindow = new()
            {
                MaxWindow = 0.15,
                MinWindow = 0.04
            };

            public FiveFretGuitarPreset Copy()
            {
                return new FiveFretGuitarPreset
                {
                    AntiGhosting = AntiGhosting,
                    InfiniteFrontEnd = InfiniteFrontEnd,
                    HopoLeniency = HopoLeniency,
                    StrumLeniency = StrumLeniency,
                    StrumLeniencySmall = StrumLeniencySmall,
                    HitWindow = HitWindow.Copy(),
                };
            }
        }

        /// <summary>
        /// The engine preset for four and five lane drums. These two game modes
        /// use the same engine, so there's no point in splitting them up.
        /// </summary>
        public class DrumsPreset
        {
            public HitWindowPreset HitWindow = new()
            {
                MaxWindow = 0.15,
                MinWindow = 0.04
            };

            public DrumsPreset Copy()
            {
                return new DrumsPreset
                {
                    HitWindow = HitWindow.Copy()
                };
            }
        }
    }
}
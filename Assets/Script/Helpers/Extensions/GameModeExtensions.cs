using YARG.Core;

namespace YARG.Helpers.Extensions
{
    public static class GameModeExtensions
    {
        public static string ToResourceName(this GameMode instrument)
        {
            return instrument switch
            {
                GameMode.FiveFretGuitar => "guitar",
                GameMode.SixFretGuitar  => "guitar",

                GameMode.FourLaneDrums  => "drums",
                GameMode.FiveLaneDrums  => "ghDrums",
                // GameMode.EliteDrums   => "eliteDrums",

                GameMode.ProGuitar      => "realGuitar",
                GameMode.ProKeys        => "realKeys",

                GameMode.Vocals         => "vocals",

                // GameMode.Dj          => "dj",

                _                       => null,
            };
        }
    }
}
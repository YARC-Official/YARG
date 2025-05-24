using YARG.Core.Game;

public static class StarSortHelper
{
    public static int GetSortWeight(StarAmount stars)
    {
        return stars switch
        {
            StarAmount.StarGold => 6,
            StarAmount.Star5 => 5,
            StarAmount.Star4 => 4,
            StarAmount.Star3 => 3,
            StarAmount.Star2 => 2,
            StarAmount.Star1 => 1,
            StarAmount.None => 0,
            _ => -1 // StarSilver, StarBrutal
        };
    }

    public static int GetSortWeightFromKey(string label)
    {
        return label switch
        {
            "Gold Stars" => 6,
            "5 Stars"    => 5,
            "4 Stars"    => 4,
            "3 Stars"    => 3,
            "2 Stars"    => 2,
            "1 Star"     => 1,
            "Not Played" => 0,
            _            => -1
        };
    }
}


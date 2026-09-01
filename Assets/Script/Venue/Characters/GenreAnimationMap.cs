using System;
using UnityEngine;
using YARG.Helpers;
using YARG.Song;

namespace YARG.Venue.Characters
{
    [Serializable]
    public class GenreAnimationMap : SerializedDictionary<Genrelizer.BaseGenre, RuntimeAnimatorController>
    {

    }
}
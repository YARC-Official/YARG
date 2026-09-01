using System;
using YARG.Helpers;

namespace YARG.Venue.Characters
{
    [Serializable]
    public class AnimationStateMap : SerializedDictionary<VenueCharacter.AnimationStateType, string>
    {
        public bool TryGetStateForName(string name, out VenueCharacter.AnimationStateType type)
        {
            var ret = false;
            type = default;

            var index = _values.IndexOf(name);

            if (index != -1)
            {
                type = _keys[index];
                ret = true;
            }

            return ret;
        }
    }
}
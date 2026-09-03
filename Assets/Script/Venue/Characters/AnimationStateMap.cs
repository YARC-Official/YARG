using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Helpers;

namespace YARG.Venue.Characters
{
    [Serializable]
    public class AnimationStateMap : SerializedDictionary<VenueCharacter.AnimationStateType, string>
    {
        [SerializeField]
        private List<VenueCharacter.AnimationStateType> _animationStateTypes = new();
        [SerializeField]
        private List<string> _animationStateNames = new();

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

        public override void OnAfterDeserialize()
        {
            // Backwards compatibility..copy backing fields to base fields when we have old format data
            if (_animationStateTypes.Count > 0 && _animationStateNames.Count > 0)
            {
                _keys = new List<VenueCharacter.AnimationStateType>(_animationStateTypes);
                _values = new List<string>(_animationStateNames);
            }

            base.OnAfterDeserialize();
        }
    }
}
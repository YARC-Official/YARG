using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace YARG.Venue.Characters
{
    [System.Serializable]
    public class AnimationStateMap : ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<VenueCharacter.AnimationStateType> _animationStateTypes = new();
        [SerializeField]
        private List<string> _animationStateNames = new();

        private Dictionary<VenueCharacter.AnimationStateType, string> _animationStateDictionary = new();
        public Dictionary<VenueCharacter.AnimationStateType, string> Dictionary => _animationStateDictionary;
        public List<string> Names => _animationStateNames;

        public void OnBeforeSerialize()
        {
            _animationStateTypes.Clear();
            _animationStateNames.Clear();

            foreach (var pair in _animationStateDictionary)
            {
                _animationStateTypes.Add(pair.Key);
                _animationStateNames.Add(pair.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            _animationStateDictionary.Clear();
            for (int i = 0; i < _animationStateTypes.Count; i++)
            {
                _animationStateDictionary.Add(_animationStateTypes[i], _animationStateNames[i]);
            }
        }


        public void Add(VenueCharacter.AnimationStateType type, string name)
        {
            _animationStateTypes.Add(type);
            _animationStateNames.Add(name);
        }

        public Dictionary<VenueCharacter.AnimationStateType, string> ToDictionary()
        {
            var output = new Dictionary<VenueCharacter.AnimationStateType, string>();
            for (int i = 0; i < _animationStateTypes.Count; i++)
            {
                output.Add(_animationStateTypes[i], _animationStateNames[i]);
            }

            return output;
        }

        public bool TryGetStateForName(string name, out VenueCharacter.AnimationStateType type)
        {
            var ret = false;
            type = default;

            var index = _animationStateNames.IndexOf(name);

            if (index != -1)
            {
                type = _animationStateTypes[index];
                ret = true;
            }

            return ret;
        }

        public void Clear()
        {
            _animationStateTypes.Clear();
            _animationStateNames.Clear();
        }

    }
}
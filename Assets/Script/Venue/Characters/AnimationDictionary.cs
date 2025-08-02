using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace YARG.Venue.Characters
{
    /// <summary>
    /// A stupid workaround for Unity's inability to serialize Dictionary&lt;&lt;T&gt;,List&lt;T&gt;&gt;
    /// </summary>

    // What we're doing here is using Unity's limited SerializedDictionary to store a layer name and a number
    // of animations in that layer and a serialized List<string> with the animation names for the layers.
    // Unity can serialize these things, but we have to go to the trouble of putting everything back together ourselves
    [Serializable]
    public class AnimationDictionary
    {
        // TODO: Make this actually work right or get rid of it, rn it's somehow trying to add the "Base Layer" key
        // to _layerCounts repeatedly.

        // In case it isn't obvious, layerCounts and layerNames are separate because layerCounts is not ordered
        [SerializeField]
        private SerializedDictionary<string, int> _layerCounts = new();
        [SerializeField]
        private List<string> _layerNames = new();
        [SerializeField]
        private List<string> _animationNames = new();

        public void Add(string layerName, string animationName)
        {
            if (!_layerNames.Contains(layerName))
            {
                _layerNames.Add(layerName);
            }

            _layerCounts.TryAdd(layerName, 0);
            _layerCounts[layerName]++;

            _animationNames.Add(animationName);

        }

        public Dictionary<string, List<string>> ToDictionary()
        {
            var dict = new Dictionary<string, List<string>>();
            int runningCount = 0;

            foreach (var name in _layerNames)
            {
                // Get the count and add that many to the list
                var count = _layerCounts[name];
                var list = new List<string>();
                for (int i = runningCount; i < count + runningCount; i++)
                {
                    list.Add(_animationNames[i]);
                }
                runningCount += count;
                dict.Add(name, list);
            }

            return dict;
        }
    }
}
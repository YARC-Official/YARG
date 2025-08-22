using System;
using System.Collections.Generic;
using UnityEngine;

namespace YARG.Venue.Characters
{
    [Serializable]
    public class LayerAnimationList
    {
        public string       layerName;
        public List<string> animationNames = new();
    }

    /// <summary>
    /// A serializable data structure to represent a dictionary of string to list of strings.
    /// This replaces the previous implementation that relied on a combination of lists and a SerializedDictionary.
    /// </summary>
    [Serializable]
    public class AnimationDictionary
    {
        [SerializeField]
        private List<LayerAnimationList> _layers = new();

        public void Add(string layerName, string animationName)
        {
            // Find an existing entry for the layer
            LayerAnimationList layerList = null;
            foreach (var layer in _layers)
            {
                if (layer.layerName == layerName)
                {
                    layerList = layer;
                    break;
                }
            }

            // If no entry exists, create a new one
            if (layerList == null)
            {
                layerList = new LayerAnimationList { layerName = layerName };
                _layers.Add(layerList);
            }

            layerList.animationNames.Add(animationName);
        }

        public void Clear()
        {
            _layers.Clear();
        }

        public Dictionary<string, List<string>> ToDictionary()
        {
            var dict = new Dictionary<string, List<string>>();
            foreach (var layer in _layers)
            {
                if (string.IsNullOrEmpty(layer.layerName))
                {
                    continue;
                }

                if (!dict.ContainsKey(layer.layerName))
                {
                    // Create a new list to avoid aliasing with the serialized list
                    dict.Add(layer.layerName, new List<string>(layer.animationNames));
                }
                else
                {
                    // If a layer with the same name was somehow added twice (e.g. via inspector), merge them.
                    dict[layer.layerName].AddRange(layer.animationNames);
                }
            }

            return dict;
        }
    }
}
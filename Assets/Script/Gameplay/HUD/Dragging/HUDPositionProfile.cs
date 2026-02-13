using System.Collections.Generic;
using UnityEngine;

namespace YARG.Gameplay.HUD
{
    public class HUDPositionProfile
    {
        public const int CURRENT_VERSION = 1;

        public int Version = CURRENT_VERSION;
        public Dictionary<string, Vector2> ElementPositions = new();
        public Dictionary<string, float> ElementScales = new();

        public bool HasElementPosition(string name)
        {
            ElementPositions ??= new();
            return ElementPositions.ContainsKey(name);
        }

        public Vector2? GetElementPosition(string name)
        {
            ElementPositions ??= new();
            if (ElementPositions.TryGetValue(name, out var position))
            {
                return position;
            }

            return null;
        }

        public void SaveElementPosition(string name, Vector2 position)
        {
            ElementPositions ??= new();
            ElementPositions[name] = position;
        }

        public void RemoveElementPosition(string name)
        {
            ElementPositions ??= new();
            ElementPositions.Remove(name);
        }

        public bool HasElementScale(string name)
        {
            ElementScales ??= new();
            return ElementScales.ContainsKey(name);
        }

        public float? GetElementScale(string name)
        {
            ElementScales ??= new();
            if (ElementScales.TryGetValue(name, out var scale))
            {
                return scale;
            }

            return null;
        }

        public void SaveElementScale(string name, float scale)
        {
            ElementScales ??= new();
            ElementScales[name] = scale;
        }

        public void RemoveElementScale(string name)
        {
            ElementScales ??= new();
            ElementScales.Remove(name);
        }
    }
}

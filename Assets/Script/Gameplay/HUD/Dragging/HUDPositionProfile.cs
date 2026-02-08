using System.Collections.Generic;
using UnityEngine;

namespace YARG.Gameplay.HUD
{
    public class HUDPositionProfile
    {
        public const int CURRENT_VERSION = 1;

        public int Version = CURRENT_VERSION;
        public Dictionary<string, Vector2> ElementPositions = new();

        public bool HasElementPosition(string name)
        {
            return ElementPositions.ContainsKey(name);
        }

        public Vector2? GetElementPosition(string name)
        {
            if (ElementPositions.TryGetValue(name, out var position))
            {
                return position;
            }

            return null;
        }

        public void SaveElementPosition(string name, Vector2 position)
        {
            ElementPositions[name] = position;
        }

        public void RemoveElementPosition(string name)
        {
            ElementPositions.Remove(name);
        }
    }
}

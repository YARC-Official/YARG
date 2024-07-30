using System.Collections.Generic;
using UnityEngine;

namespace YARG.Gameplay.HUD
{
    public class HUDPositionProfile
    {
        public const int CURRENT_VERSION = 1;

        public int Version = CURRENT_VERSION;
        public Dictionary<string, Vector2> ElementPositions = new();

        public Vector2 GetElementPositionOrDefault(string name, Vector2 defaultPosition)
        {
            if (ElementPositions.TryGetValue(name, out var position))
            {
                return position;
            }

            ElementPositions.Add(name, defaultPosition);
            return defaultPosition;
        }

        public void SaveElementPosition(string name, Vector2 position)
        {
            ElementPositions[name] = position;
        }
    }
}
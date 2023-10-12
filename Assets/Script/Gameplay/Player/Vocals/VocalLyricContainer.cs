using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Visuals;

namespace YARG.Gameplay.Player
{
    public class VocalLyricContainer : MonoBehaviour
    {
        private const float LYRIC_SPACING = 0.25f;

        [SerializeField]
        private Pool _pool;

        private double _lastLyricEdgeTime = double.NegativeInfinity;

        public bool TrySpawnLyric(TextEvent lyric, VocalNote probableNotePair, bool isStarpower)
        {
            // Skip this frame if the pool is full
            if (!_pool.CanSpawnAmount(1))
            {
                return false;
            }

            // Get the length and starpower (starpower is a phrase property)
            double length = probableNotePair?.TotalTimeLength ?? 0;

            // Spawn the vocal lyric
            var obj = (VocalLyricElement) _pool.TakeWithoutEnabling();
            obj.Initialize(lyric, _lastLyricEdgeTime, length, isStarpower);
            obj.EnableFromPool();

            // Set the edge time
            _lastLyricEdgeTime = obj.ElementTime + (obj.Width + LYRIC_SPACING) / VocalTrack.NOTE_SPEED;

            return true;
        }
    }
}
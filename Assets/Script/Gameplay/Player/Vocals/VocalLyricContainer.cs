using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Visuals;

namespace YARG.Gameplay.Player
{
    public class VocalLyricContainer : MonoBehaviour
    {
        private const float LYRIC_SPACING = 0.25f;

        [Header("Index 0 should be bottom, 2 should be top.")]
        [SerializeField]
        private Pool[] _pools;

        private readonly double[] _lastLyricEdgeTime =
        {
            double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity
        };

        public float TrackSpeed { get; set; }

        public bool TrySpawnLyric(LyricEvent lyric, VocalNote probableNotePair, bool isStarpower, int harmIndex)
        {
            // Skip this frame if the pool is full
            if (!_pools[harmIndex].CanSpawnAmount(1))
            {
                return false;
            }

            // Get the info from the probably note pair, IF it exists
            double length = probableNotePair?.TotalTimeLength ?? 0;

            // Spawn the vocal lyric
            var obj = (VocalLyricElement) _pools[harmIndex].TakeWithoutEnabling();
            obj.Initialize(lyric, _lastLyricEdgeTime[harmIndex], length, isStarpower, harmIndex);
            obj.EnableFromPool();

            // Set the edge time
            _lastLyricEdgeTime[harmIndex] = obj.ElementTime + (obj.Width + LYRIC_SPACING) / TrackSpeed;

            return true;
        }

        public void ResetVisuals()
        {
            _lastLyricEdgeTime[0] = double.NegativeInfinity;
            _lastLyricEdgeTime[1] = double.NegativeInfinity;
            _lastLyricEdgeTime[2] = double.NegativeInfinity;

            foreach (var pool in _pools)
            {
                pool.ReturnAllObjects();
            }
        }
    }
}
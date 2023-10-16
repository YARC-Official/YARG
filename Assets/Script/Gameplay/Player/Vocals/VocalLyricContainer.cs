using UnityEngine;
using UnityEngine.Serialization;
using YARG.Core.Chart;
using YARG.Gameplay.Visuals;

namespace YARG.Gameplay.Player
{
    public class VocalLyricContainer : MonoBehaviour
    {
        private const float LYRIC_SPACING = 0.25f;

        [Header("Index 0 should be bottom, 1 should be top.")]
        [SerializeField]
        private Pool[] _pools;

        private readonly double[] _lastLyricEdgeTime =
        {
            double.NegativeInfinity, double.NegativeInfinity
        };

        private string _lastSecondHarmonyLyric;

        public bool TrySpawnLyric(TextEvent lyric, VocalNote probableNotePair, bool isStarpower, int harmIndex)
        {
            // Index should be 0 or 1
            int i = harmIndex;
            if (i > 1) i = 1;

            // Never show HARM3's lyrics unless they're different from HARM2's lyric
            if (harmIndex == 2 && _lastSecondHarmonyLyric == lyric.Text)
            {
                _lastSecondHarmonyLyric = null;
                return true;
            }

            // Skip this frame if the pool is full
            if (!_pools[i].CanSpawnAmount(1))
            {
                return false;
            }

            // Get the length and starpower (starpower is a phrase property)
            double length = probableNotePair?.TotalTimeLength ?? 0;

            // Spawn the vocal lyric
            var obj = (VocalLyricElement) _pools[i].TakeWithoutEnabling();
            obj.Initialize(lyric, _lastLyricEdgeTime[i], length, isStarpower, harmIndex);
            obj.EnableFromPool();

            // Set the edge time
            _lastLyricEdgeTime[i] = obj.ElementTime + (obj.Width + LYRIC_SPACING) / VocalTrack.NOTE_SPEED;

            // Prevent duplicates on HARM3
            if (harmIndex == 1)
            {
                _lastSecondHarmonyLyric = lyric.Text;
            }

            return true;
        }
    }
}
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Visuals;
using YARG.Settings;

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

        private string _lastSecondHarmonyLyric;

        public bool TrySpawnLyric(LyricEvent lyric, VocalNote probableNotePair, bool isStarpower, int harmIndex)
        {
            var combineHarmonyLyrics = !SettingsManager.Settings.UseThreeLaneLyricsInHarmony.Value;

            // Choose the correct lane for the lyrics
            int lane = harmIndex;
            if (combineHarmonyLyrics && lane == 1)
            {
                // In two lane mode, the middle track should not be used
                lane = 2;
            }

            // When combining lyrics, never show HARM3's lyrics unless they're different from HARM2's lyric
            if (combineHarmonyLyrics && harmIndex == 2 && _lastSecondHarmonyLyric == lyric.Text)
            {
                _lastSecondHarmonyLyric = null;
                return true;
            }

            // Skip this frame if the pool is full
            if (!_pools[lane].CanSpawnAmount(1))
            {
                return false;
            }

            // Get the info from the probably note pair, IF it exists
            double length = probableNotePair?.TotalTimeLength ?? 0;

            // Spawn the vocal lyric
            var obj = (VocalLyricElement) _pools[lane].TakeWithoutEnabling();
            obj.Initialize(lyric, _lastLyricEdgeTime[lane], length, isStarpower, harmIndex);
            obj.EnableFromPool();

            // Set the edge time
            _lastLyricEdgeTime[lane] = obj.ElementTime + (obj.Width + LYRIC_SPACING) / TrackSpeed;

            // When combining lyrics, prevent duplicates on HARM3
            if (combineHarmonyLyrics && harmIndex == 1)
            {
                _lastSecondHarmonyLyric = lyric.Text;
            }

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
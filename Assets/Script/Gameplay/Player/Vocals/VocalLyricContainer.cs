using System;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Visuals;
using YARG.Settings;
using static YARG.Gameplay.Player.VocalTrack;

namespace YARG.Gameplay.Player
{
    public class VocalLyricContainer : MonoBehaviour
    {
        public const float LYRIC_SPACING = 0.25f;
        public const float STATIC_PHRASE_SPACING = .5f;

        [Header("Index 0 should be bottom, 2 should be top.")]
        [SerializeField]
        private Pool[] _scrollingPools;

        [Header("Index 0 should be bottom, 2 should be top.")]
        [SerializeField]
        private Pool[] _staticPools;


        private readonly double[] _lastLyricEdgeTime =
        {
            double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity
        };

        public float TrackSpeed { get; set; }

        private string _lastSecondHarmonyLyric;

        public bool TrySpawnScrollingLyric(LyricEvent lyric, VocalNote probableNotePair, bool isStarpower, int harmIndex)
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
            if (!_scrollingPools[lane].CanSpawnAmount(1))
            {
                return false;
            }

            // Get the info from the probably note pair, IF it exists
            double length = probableNotePair?.TotalTimeLength ?? 0;

            // Spawn the vocal lyric
            bool allowHiding = harmIndex != 0 && combineHarmonyLyrics;
            var obj = (VocalScrollingLyricSyllableElement) _scrollingPools[lane].TakeWithoutEnabling();
            obj.Initialize(lyric, _lastLyricEdgeTime[lane], length, isStarpower, harmIndex, allowHiding);
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

        public VocalStaticLyricPhraseElement? TrySpawnStaticLyricPhrase(VocalPhrasePair phrasePair, List<VocalsPhrase> scoringPhrases,
            int harmIndex, float x)
        {
            var combineHarmonyLyrics = !SettingsManager.Settings.UseThreeLaneLyricsInHarmony.Value;

            int laneIndex;

            if (combineHarmonyLyrics)
            {
                laneIndex = harmIndex switch
                {
                    0 => 0,
                    1 => 2,
                    2 => 2,
                    _ => throw new InvalidOperationException("Unexpected lyric lane count")
                };
            } else
            {
                laneIndex = harmIndex;
            }

            // Skip this frame if the pool is full
            if (!_staticPools[laneIndex].CanSpawnAmount(1))
            {
                return null;
            }

            // Spawn the vocal lyric
            bool allowHiding = harmIndex != 0 && combineHarmonyLyrics;
            var obj = (VocalStaticLyricPhraseElement) _staticPools[laneIndex].TakeWithoutEnabling();
            obj.Initialize(phrasePair, scoringPhrases, laneIndex, allowHiding, x);
            obj.EnableFromPool();

            return obj;
        }

        public void ResetVisuals()
        {
            _lastLyricEdgeTime[0] = double.NegativeInfinity;
            _lastLyricEdgeTime[1] = double.NegativeInfinity;
            _lastLyricEdgeTime[2] = double.NegativeInfinity;

            foreach (var pool in _scrollingPools)
            {
                pool.ReturnAllObjects();
            }

            foreach (var pool in _staticPools)
            {
                pool.ReturnAllObjects();
            }
        }
    }
}
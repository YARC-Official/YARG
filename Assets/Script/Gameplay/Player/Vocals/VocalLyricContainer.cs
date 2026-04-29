using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Visuals;
using YARG.Settings;
using static YARG.Gameplay.Player.VocalTrack;

namespace YARG.Gameplay.Player
{
    public class VocalLyricContainer : MonoBehaviour
    {
        public enum LyricSpawnResult
        {
            Spawned,
            Suppressed,
            PoolUnavailable
        }

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
        private TextMeshPro[] _scrollingWidthTesters;
        private TextMeshPro[] _staticWidthTesters;

        public static int GetLaneIndex(int totalHarms, int harmIndex)
        {
            var combineHarmonyLyrics = !SettingsManager.Settings.UseThreeLaneLyricsInHarmony.Value;

            if (combineHarmonyLyrics || totalHarms == 2)
            {
                return harmIndex switch
                {
                    0 => 0,
                    1 => 2,
                    2 => 2,
                    _ => throw new InvalidOperationException("Unexpected lyric lane count")
                };
            }

            return harmIndex;
        }

        public void PrewarmScrollingPool(int laneIndex, int targetTotalCount)
        {
            _scrollingPools[laneIndex].PrewarmTo(targetTotalCount);
        }

        public void PrewarmStaticPool(int laneIndex, int targetTotalCount)
        {
            _staticPools[laneIndex].PrewarmTo(targetTotalCount);
        }

        public VocalScrollingLyricSyllableElement.PreparedLyric PrepareScrollingLyric(
            LyricEvent lyric, VocalNote probableNote, int totalHarms, int harmIndex)
        {
            var combineHarmonyLyrics = !SettingsManager.Settings.UseThreeLaneLyricsInHarmony.Value;
            bool allowHiding = harmIndex != 0 && combineHarmonyLyrics;
            string displayText = lyric.HarmonyHidden && allowHiding ? string.Empty : lyric.Text;

            float width = 0f;
            if (!string.IsNullOrEmpty(displayText))
            {
                var tester = GetScrollingWidthTester(GetLaneIndex(totalHarms, harmIndex));
                tester.fontStyle = lyric.NonPitched ? FontStyles.Italic : FontStyles.Normal;
                tester.text = displayText;
                width = tester.GetPreferredValues().x;
            }

            return new VocalScrollingLyricSyllableElement.PreparedLyric(lyric, probableNote, allowHiding, width);
        }

        public VocalStaticLyricPhraseElement.PreparedPhrase PrepareStaticLyricPhrase(
            VocalPhrasePair phrasePair, List<VocalsPhrase> scoringPhrases, int totalHarms, int harmIndex)
        {
            var combineHarmonyLyrics = !SettingsManager.Settings.UseThreeLaneLyricsInHarmony.Value;
            bool allowHiding = harmIndex != 0 && combineHarmonyLyrics;
            var preparedPhrase = new VocalStaticLyricPhraseElement.PreparedPhrase(
                phrasePair, scoringPhrases, allowHiding, 0f);

            var tester = GetStaticWidthTester(GetLaneIndex(totalHarms, harmIndex));
            tester.fontStyle = FontStyles.Normal;
            tester.text = preparedPhrase.FutureText;
            var width = tester.GetPreferredValues().x;

            return preparedPhrase.WithWidth(width);
        }

        public LyricSpawnResult TrySpawnScrollingLyric(VocalScrollingLyricSyllableElement.PreparedLyric preparedLyric,
            bool isStarpower, int totalHarms, int harmIndex)
        {
            var combineHarmonyLyrics = !SettingsManager.Settings.UseThreeLaneLyricsInHarmony.Value;
            var lyric = preparedLyric.Lyric;
            int laneIndex = GetLaneIndex(totalHarms, harmIndex);

            // When combining lyrics, never show HARM3's lyrics unless they're different from HARM2's lyric
            if (combineHarmonyLyrics && harmIndex == 2 && _lastSecondHarmonyLyric == lyric.Text)
            {
                _lastSecondHarmonyLyric = null;
                return LyricSpawnResult.Suppressed;
            }

            if (preparedLyric.IsHidden)
            {
                _lastLyricEdgeTime[laneIndex] = Math.Max(lyric.Time, _lastLyricEdgeTime[laneIndex]) +
                    LYRIC_SPACING / TrackSpeed;

                if (combineHarmonyLyrics && harmIndex == 1)
                {
                    _lastSecondHarmonyLyric = lyric.Text;
                }

                return LyricSpawnResult.Suppressed;
            }

            // Skip this frame if the pool is full
            if (!_scrollingPools[laneIndex].CanSpawnAmount(1))
            {
                return LyricSpawnResult.PoolUnavailable;
            }

            // Spawn the vocal lyric
            bool allowHiding = harmIndex != 0 && combineHarmonyLyrics;
            var obj = (VocalScrollingLyricSyllableElement) _scrollingPools[laneIndex].TakeWithoutEnabling();
            obj.Initialize(preparedLyric, _lastLyricEdgeTime[laneIndex], preparedLyric.NoteLength, isStarpower, harmIndex, allowHiding);
            obj.EnableFromPool();

            // Set the edge time
            _lastLyricEdgeTime[laneIndex] = obj.ElementTime + (preparedLyric.Width + LYRIC_SPACING) / TrackSpeed;

            // When combining lyrics, prevent duplicates on HARM3
            if (combineHarmonyLyrics && harmIndex == 1)
            {
                _lastSecondHarmonyLyric = lyric.Text;
            }

            return LyricSpawnResult.Spawned;
        }

        #nullable enable
        public VocalStaticLyricPhraseElement? TrySpawnStaticLyricPhrase(
            VocalStaticLyricPhraseElement.PreparedPhrase preparedPhrase, int totalHarms, int harmIndex, float x)
        {
            int laneIndex = GetLaneIndex(totalHarms, harmIndex);

            // Skip this frame if the pool is full
            if (!_staticPools[laneIndex].CanSpawnAmount(1))
            {
                return null;
            }

            // Spawn the vocal lyric
            var obj = (VocalStaticLyricPhraseElement) _staticPools[laneIndex].TakeWithoutEnabling();
            obj.Initialize(preparedPhrase, x);
            obj.EnableFromPool();

            return obj;
        }
        #nullable disable

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

        private TextMeshPro GetScrollingWidthTester(int laneIndex)
        {
            _scrollingWidthTesters ??= new TextMeshPro[_scrollingPools.Length];
            return _scrollingWidthTesters[laneIndex] ??=
                CreateWidthTester(_scrollingPools[laneIndex].Prefab, $"Scrolling Lyric Width Tester {laneIndex}");
        }

        private TextMeshPro GetStaticWidthTester(int laneIndex)
        {
            _staticWidthTesters ??= new TextMeshPro[_staticPools.Length];
            return _staticWidthTesters[laneIndex] ??=
                CreateWidthTester(_staticPools[laneIndex].Prefab, $"Static Lyric Width Tester {laneIndex}");
        }

        private TextMeshPro CreateWidthTester(GameObject prefab, string name)
        {
            var instance = Instantiate(prefab, transform);
            instance.name = name;
            instance.SetActive(false);

            return instance.GetComponentInChildren<TextMeshPro>(true);
        }
    }
}

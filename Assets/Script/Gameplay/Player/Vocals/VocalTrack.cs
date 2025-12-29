using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Menu.Persistent;
using YARG.Player;
using YARG.Settings;

namespace YARG.Gameplay.Player
{
    public partial class VocalTrack : GameplayBehaviour
    {
        private struct Range
        {
            public float Min;
            public float Max;

            public Range(float min, float max)
            {
                Min = min;
                Max = max;
            }

            public Range(VocalsRangeShift range)
            {
                // Pad out range based on note width
                float minPitch = range.MinimumPitch - NOTE_WIDTH_MULTIPLIER / 2;
                float maxPitch = range.MaximumPitch + NOTE_WIDTH_MULTIPLIER / 2;

                // Ensure range is at least a minimum size
                float rangeMiddle = (range.MaximumPitch + range.MinimumPitch) / 2;
                Min = Math.Min(rangeMiddle - (MINIMUM_SEMITONE_RANGE / 2), minPitch);
                Max = Math.Max(rangeMiddle + (MINIMUM_SEMITONE_RANGE / 2), maxPitch);

                var rangePadding = (Max - Min) * RANGE_PADDING_PERCENT;
                Min -= rangePadding;
                Max += rangePadding;
            }
        }

        private static readonly int _alphaMultiplier = Shader.PropertyToID("AlphaMultiplier");

        // TODO: Temporary until color profiles for vocals
        public static readonly Color[] Colors =
        {
            new(0f, 0.800f, 1f, 1f),
            new(1f, 0.522f, 0f, 1f),
            new(1f, 0.859f, 0f, 1f)
        };

        /// <summary>
        /// Time offset relative to 1.0 note speed
        /// </summary>
        public const float SPAWN_TIME_OFFSET = 25f;

        public float SpawnTimeOffset => SPAWN_TIME_OFFSET / TrackSpeed;

        // NOTE: all of these top and bottom edge variables are relative to the note spawners
        // which are off by 0.07 units. I'm not entirely sure where this offset originated from,
        // however it should eventually be fixed.

        /// <summary>
        /// The top edge of the vocal highway track when playing with 1 lyric lane
        /// </summary>
        private const float TRACK_TOP_ONE_LANE = 1.25f;

        /// <summary>
        /// The top edge of the vocal highway track when playing with 2 lyric lanes
        /// </summary>
        private const float TRACK_TOP_TWO_LANE = 0.93f;

        /// <summary>
        /// The top edge of the vocal highway track when playing with 3 lyric lanes
        /// </summary>
        private const float TRACK_TOP_THREE_LANE = 0.64f;

        /// <summary>
        /// The bottom edge of the vocal highway track
        /// </summary>
        private const float TRACK_BOTTOM = -0.63f;

        /// <summary>
        /// The amount of additional padding to apply to the visible semi-tone range, expressed as a percentage.
        /// </summary>
        private const float RANGE_PADDING_PERCENT = 0.1f;

        private const float NOTE_WIDTH_MULTIPLIER = 1.5f;

        /// <summary>
        /// The minimum vocal range that should be displayed on the vocal highway, in semi-tones.
        /// </summary>
        private const float MINIMUM_SEMITONE_RANGE = 20;

        /// <summary>
        /// The minimum amount of time a vocal range shift should take, in seconds.
        /// </summary>
        private const double MINIMUM_SHIFT_TIME = 0.25;

        /// <summary>
        /// The standard scaling factor for vocal scroll speeds.
        /// </summary>
        private const float STANDARD_SCROLL_SPEED = 5f;

        /// <summary>
        /// The minimum allowed aspect ratio for the vocal track
        /// </summary>
        private const float MIN_ASPECT_RATIO = 8.0f;

        /// <summary>
        /// The maximum allowed aspect ratio for the vocal track
        /// </summary>
        private const float MAX_ASPECT_RATIO = 13.0f;

        [SerializeField]
        private VocalsPlayer _vocalPlayerPrefab;
        [SerializeField]
        private VocalPercussionTrack _percussionTrackPrefab;

        [Space]
        [SerializeField]
        private MeshRenderer _trackRenderer;
        [SerializeField]
        private Material[] _trackMaterials;

        [Space]
        [SerializeField]
        private MeshRenderer _oneLaneGuidelineRenderer;
        [SerializeField]
        private MeshRenderer _twoLaneGuidelineRenderer;
        [SerializeField]
        private MeshRenderer _threeLaneGuidelineRenderer;

        [Space]
        [SerializeField]
        private MeshRenderer _oneLaneStarpowerOverlay;
        [SerializeField]
        private MeshRenderer _twoLaneStarpowerOverlay;
        [SerializeField]
        private MeshRenderer _threeLaneStarpowerOverlay;

        [Space]
        [SerializeField]
        private CountdownDisplay _countdownDisplay;

        [Space]
        [SerializeField]
        private Camera _trackCamera;
        [SerializeField]
        private Transform _playerContainer;
        [SerializeField]
        private Transform _percussionTrackContainer;
        [SerializeField]
        private VocalLyricContainer _lyricContainer;

        [Space]
        [SerializeField]
        private Pool[] _notePools;
        [SerializeField]
        private Pool _talkiePool;
        [SerializeField]
        private Pool _phraseLinePool;

        private readonly List<VocalsPlayer> _vocalPlayers = new();

        private float _currentTrackTop;
        private Material _starpowerMaterial;

        private VocalsTrack _originalVocalsTrack;
        private VocalsTrack _vocalsTrack;

        private Material _guidelineMaterial;

        private bool _isRangeChanging;
        private Range _viewRange;
        private Range _targetRange;
        private Range _previousRange;

        private int _nextRangeIndex = 1;
        private double _changeStartTime;
        private double _changeEndTime;

        private static int _totalHarms;

        public float TrackSpeed { get; private set; }

        public int LyricLaneCount { get; private set; }

        [HideInInspector]
        public bool AllowStarPower;

        public float CurrentNoteWidth =>
            ((_currentTrackTop - TRACK_BOTTOM) / (_viewRange.Max - _viewRange.Min)) * NOTE_WIDTH_MULTIPLIER;

        private void Start()
        {
            Assert.AreEqual(_notePools.Length, 3,
                "Note pools must be of length three (one for each harmony part).");
        }

        public void InitializeRenderTexture(RectTransform vocalsImage, RenderTexture renderTexture)
        {
            var vocalsSize = vocalsImage.ToScreenSpace();
            var imageAspectRatio = vocalsSize.width / vocalsSize.height;
            var clampedAspectRatio = Math.Clamp(imageAspectRatio, MIN_ASPECT_RATIO, MAX_ASPECT_RATIO);

            float heightPixels = vocalsSize.height;
            float widthPixels = heightPixels * clampedAspectRatio;

            // Convert to normalized coordinates
            float heightNormalized = heightPixels / Screen.height;
            float widthNormalized = widthPixels / Screen.width;

            // Center horizontally and position below stats overlay
            float xPos = (1.0f - widthNormalized) / 2.0f;
            var statsHeightNormalized = StatsManager.Instance.GetComponent<RectTransform>().ToScreenSpace().height / Screen.height;
            float yPos = 1.0f - heightNormalized - statsHeightNormalized;

            _trackCamera.rect = new Rect(xPos, yPos, widthNormalized, heightNormalized);
            _trackCamera.targetTexture = renderTexture;
        }

        public void Initialize(VocalsTrack vocalsTrack, YargPlayer primaryPlayer, float? trackSpeed)
        {
            _originalVocalsTrack = vocalsTrack;

            // Apply the modifiers of the primary player. All players should have the
            // same modifier(s) chosen.
            foreach (var part in _originalVocalsTrack.Parts)
            {
                primaryPlayer.Profile.ApplyVocalModifiers(part);
            }

            _vocalsTrack = _originalVocalsTrack.Clone();

            float scalingFactor;

            // If the chart provided a vocal scrolling speed, use it. Note that the default value of 2300 in DTAs
            // is treated as no value.
            if (trackSpeed is not null)
            {
                scalingFactor = trackSpeed.Value;
            }

            // If we're in scrolling lyrics mode and weren't provided a vocal scroll speed, determine if we need
            // to increase the speed to keep the lyrics from being pushed too far out of sync.
            else if (!SettingsManager.Settings.StaticVocalsMode.Value)
            {
                scalingFactor = GetScrollSpeedScalingFactor(vocalsTrack.Parts);
            }

            // If we're in static lyrics mode, we don't need to worry about checking the lyric offsets.
            else
            {
                scalingFactor = 1f;
            }

            TrackSpeed = scalingFactor * STANDARD_SCROLL_SPEED;

            _lyricContainer.TrackSpeed = TrackSpeed;

            // Reset first
            _totalHarms = 0;

            // Get the number of harmony parts in the song
            var parts = _vocalsTrack.Parts;
            foreach (var part in parts)
            {
                if (part.IsHarmony && part.NotePhrases.Count > 0)
                {
                    _totalHarms++;
                }
            }

            // Choose the correct amount of lanes
            LyricLaneCount = 1;
            if (vocalsTrack.Instrument == Instrument.Harmony)
            {
                LyricLaneCount = _totalHarms switch
                {
                    1 => 1, // Just in case there's a 1-harm chart
                    2 => 2,
                    3 => SettingsManager.Settings.UseThreeLaneLyricsInHarmony.Value ? 3 : 2,
                    _ => 3,
                };
            }

            // Create trackers and indices
            _phraseMarkerIndices = new int[parts.Count];
            _scrollingNoteTrackers = new ScrollingPhraseNoteTracker[parts.Count];
            _scrollingLyricTrackers = new ScrollingPhraseNoteTracker[parts.Count];
            _staticPhraseTrackers = new StaticPhraseTracker[parts.Count];
            _staticPhraseQueues = new Queue<VocalStaticLyricPhraseElement>[parts.Count];



            // Create PhraseNoteTrackers
            for (int i = 0; i < parts.Count; i++)
            {
                _scrollingNoteTrackers[i] = new ScrollingPhraseNoteTracker(parts[i], false);
                _scrollingLyricTrackers[i] = new ScrollingPhraseNoteTracker(parts[i], true);

                if (SettingsManager.Settings.UseThreeLaneLyricsInHarmony.Value)
                {
                    // If we're in 3-lane mode, just give each lane its own tracker with no merging
                    _staticPhraseTrackers[i] = new StaticPhraseTracker(GetVocalPhrasePairs(parts[i], null));
                }
                else
                {
                    // If we're in 2-lane mode...
                    switch (i)
                    {
                        case 0:
                            // ...HARM1 gets its own tracker with no merging...
                            _staticPhraseTrackers[i] = new StaticPhraseTracker(GetVocalPhrasePairs(parts[i], null));
                            break;
                        case 1:
                            // ...but HARM2 gets HARM3 as a merged part
                            _staticPhraseTrackers[i] = new StaticPhraseTracker(GetVocalPhrasePairs(parts[i], parts[i+1]));
                            break;
                        // Do nothing for HARM3, because it's being handled by HARM2
                    }
                }
                _staticPhraseQueues[i] = new Queue<VocalStaticLyricPhraseElement>();
            }

            // Set the correct track material and track top constant
            _trackRenderer.material = _trackMaterials[LyricLaneCount - 1];
            _currentTrackTop = LyricLaneCount switch
            {
                1 => TRACK_TOP_ONE_LANE,
                2 => TRACK_TOP_TWO_LANE,
                3 => TRACK_TOP_THREE_LANE,
                _ => throw new Exception("Unreachable.")
            };

            // Initialize the starpower overlays
            _oneLaneStarpowerOverlay.gameObject.SetActive(LyricLaneCount == 1);
            _twoLaneStarpowerOverlay.gameObject.SetActive(LyricLaneCount == 2);
            _threeLaneStarpowerOverlay.gameObject.SetActive(LyricLaneCount == 3);
            _starpowerMaterial = LyricLaneCount switch
            {
                1 => _oneLaneStarpowerOverlay.material,
                2 => _twoLaneStarpowerOverlay.material,
                3 => _threeLaneStarpowerOverlay.material,
                _ => throw new Exception("Unreachable.")
            };

            // Initialize the guideline renderers
            _oneLaneGuidelineRenderer.gameObject.SetActive(LyricLaneCount == 1);
            _twoLaneGuidelineRenderer.gameObject.SetActive(LyricLaneCount == 2);
            _threeLaneGuidelineRenderer.gameObject.SetActive(LyricLaneCount == 3);
            _guidelineMaterial = LyricLaneCount switch
            {
                1 => _oneLaneGuidelineRenderer.material,
                2 => _twoLaneGuidelineRenderer.material,
                3 => _threeLaneGuidelineRenderer.material,
                _ => throw new Exception("Unreachable.")
            };

            // this should never happen, yell in the logs if it does
            if (_vocalsTrack.RangeShifts.Count < 1)
            {
                YargLogger.Fail("No vocal range shifts were calculated!");
                _vocalsTrack.RangeShifts.Add(new(48, 72, 0, 0, 0, 0));
            }

            // Set pitch range
            SetRange(_vocalsTrack.RangeShifts[0]);

            // Hide overlay
            _starpowerMaterial.SetFloat(_alphaMultiplier, 0f);

            AllowStarPower = true;
        }

        public VocalsPlayer CreatePlayer()
        {
            var player = Instantiate(_vocalPlayerPrefab, _playerContainer);
            _vocalPlayers.Add(player);

            return player;
        }

        public VocalPercussionTrack CreatePercussionTrack()
        {
            var percussionTrack = Instantiate(_percussionTrackPrefab, _percussionTrackContainer);

            // Space out the percussion tracks evenly
            const float FULL_HEIGHT = TRACK_TOP_ONE_LANE - TRACK_BOTTOM;
            var offset = FULL_HEIGHT / (_percussionTrackContainer.childCount + 1);
            for (int i = 0; i < _percussionTrackContainer.childCount; i++)
            {
                var child = _percussionTrackContainer.GetChild(i);
                child.localPosition = child.localPosition.WithZ(TRACK_TOP_ONE_LANE - offset * (i + 1));
            }

            return percussionTrack;
        }

        public void UpdateCountdown(double countdownLength, double endTime)
        {
            if (_countdownDisplay == null)
            {
                return;
            }

            _countdownDisplay.UpdateCountdown(countdownLength, endTime);
        }

        private void Update()
        {
            double time = GameManager.VisualTime;

            // Handle range changes
            var ranges = _vocalsTrack.RangeShifts;
            while (_nextRangeIndex < ranges.Count && ranges[_nextRangeIndex].Time < time)
            {
                StartRangeChange(ranges[_nextRangeIndex]);
                _nextRangeIndex++;
            }

            // Update the range
            if (_isRangeChanging)
            {
                float changePercent = (float) YargMath.InverseLerpD(_changeStartTime, _changeEndTime, time);

                if (changePercent >= 1f)
                {
                    // If the change has finished, stop!
                    _isRangeChanging = false;
                    _viewRange.Min = _targetRange.Min;
                    _viewRange.Max = _targetRange.Max;
                    UpdateHighwayGuidelines();
                }
                else
                {
                    float newMin = Mathf.Lerp(_previousRange.Min, _targetRange.Min, changePercent);
                    float newMax = Mathf.Lerp(_previousRange.Max, _targetRange.Max, changePercent);

                    _viewRange.Min = newMin;
                    _viewRange.Max = newMax;
                    UpdateHighwayGuidelines();
                }

                // Update notes to match new range values
                // Doing this in VocalNoteElement.UpdateElement is less reliable
                // and doesn't correctly update in the last frame of the range shift
                foreach (var pool in _notePools)
                {
                    foreach (var pooled in pool.AllSpawned)
                    {
                        if (pooled is not VocalNoteElement note)
                            continue;

                        note.UpdateLinePoints();
                    }
                }
            }

            // Try to spawn lyrics and notes
            UpdateSpawning();

            // Fade on/off the starpower overlay
            bool starpowerActive = _vocalPlayers.Any(player => player.Engine.EngineStats.IsStarPowerActive);
            float currentStarpower = _starpowerMaterial.GetFloat(_alphaMultiplier);
            if (starpowerActive)
            {
                _starpowerMaterial.SetFloat(_alphaMultiplier,
                    Mathf.Lerp(currentStarpower, 1f, Time.deltaTime * 2f));
            }
            else
            {
                _starpowerMaterial.SetFloat(_alphaMultiplier,
                    Mathf.Lerp(currentStarpower, 0f, Time.deltaTime * 4f));
            }
        }

        private void UpdateHighwayGuidelines()
        {
            const int DEFAULT_GUIDELINE_SCALE = 24;     // The semi-tone range of the guideline texture

            var scale = (_viewRange.Max - _viewRange.Min) / DEFAULT_GUIDELINE_SCALE;
            var offset = (_viewRange.Min % DEFAULT_GUIDELINE_SCALE) / DEFAULT_GUIDELINE_SCALE;
            _guidelineMaterial.mainTextureOffset = new Vector2(1, offset);
            _guidelineMaterial.mainTextureScale = new Vector2(1, scale);
        }

        private void SetRange(VocalsRangeShift range)
        {
            _previousRange = _viewRange;
            _targetRange = new Range(range);
            _viewRange = _targetRange;

            _changeStartTime = range.Time;
            _changeEndTime = range.Time;
            _isRangeChanging = false;

            UpdateHighwayGuidelines();
        }

        private void StartRangeChange(VocalsRangeShift range)
        {
            _previousRange = _viewRange;
            _targetRange = new Range(range);

            _changeStartTime = range.Time;
            _changeEndTime = range.Time + Math.Max(MINIMUM_SHIFT_TIME, range.TimeLength);
            _isRangeChanging = true;

            // UpdateHighwayGuidelines() is not needed here as it is handled in Update().
        }

        public float GetPosForTime(double time)
        {
            return (float) time * TrackSpeed;
        }

        public float GetPosForPitch(float pitch)
        {
            float rangePercent = Mathf.InverseLerp(_viewRange.Min, _viewRange.Max, pitch);
            var trackPosition = Mathf.Lerp(TRACK_BOTTOM, _currentTrackTop, rangePercent);
            return Mathf.Clamp(trackPosition, TRACK_BOTTOM, _currentTrackTop);
        }

        public void ResetPracticeSection()
        {
            // Skip if no vocals
            if (!gameObject.activeSelf) return;

            // Reset indices
            for (int i = 0; i < _scrollingNoteTrackers.Length; i++)
            {
                _phraseMarkerIndices[i] = 0;
                _scrollingNoteTrackers[i].Reset();
                _scrollingLyricTrackers[i].Reset();
                _staticPhraseTrackers[i].Reset();
                _staticPhraseQueues[i].Clear();
                _highestEnqueuedPhrasePairIndices[i] = -1;
                _rightEdges[i] = DEFAULT_STATIC_LYRICS_RIGHT_EDGE;
                _noMoreStaticPhrases[i] = false;
            }


            // Return everything
            foreach (var pool in _notePools)
            {
                pool.ReturnAllObjects();
            }

            _lyricContainer.ResetVisuals();
            _talkiePool.ReturnAllObjects();

            // Reset pitch range
            // SetPracticeSection() already takes care of removing irrelevant ranges,
            // so we can just use the first range here
            _nextRangeIndex = 1;
            SetRange(_vocalsTrack.RangeShifts[0]);
        }

        public void SetPracticeSection(uint start, uint end)
        {
            // Skip if no vocals
            if (!gameObject.activeSelf)
            {
                return;
            }

            _vocalsTrack = _originalVocalsTrack.Clone();

            // Remove all events not in the section
            for (int i = 0; i < _vocalsTrack.Parts.Count; i++)
            {
                var part = _vocalsTrack.Parts[i];
                part.NotePhrases.RemoveAll(n => n.Tick < start || n.Tick >= end);
                part.TextEvents.RemoveAll(n => n.Tick < start || n.Tick >= end);

                _scrollingNoteTrackers[i] = new(part, false);
                _scrollingLyricTrackers[i] = new(part, true);
            }

            for (int i = 0; i < LyricLaneCount; i++)
            {
                var phrasePairs = _staticPhraseTrackers[i].PhrasePairs;
                phrasePairs.RemoveAll(n => n.Tick < start || n.Tick >= end);

                _staticPhraseTrackers[i] = new(phrasePairs);
                _staticPhraseQueues[i].Clear();
            }

            // The most recent range shift before the start tick should still be preserved
            uint rangesStart = _vocalsTrack.RangeShifts.LowerBoundElement(start).Tick;
            _vocalsTrack.RangeShifts.RemoveAll(n => n.Tick < rangesStart || n.Tick >= end);

            ResetPracticeSection();
        }

        // Should only be used when the chart did not provide an explicit vocal scroll speed. Finds the largest distance
        // between a note tube and its associated lyric element (computed with respect to the default scroll speed). If
        // that distance is too big, returns an increased vocal scroll speed
        private float GetScrollSpeedScalingFactor(List<VocalsPart> parts)
        {
            var textWidthTester = gameObject.AddComponent<TextMeshPro>();

            const float DEFAULT_TRACK_SPEED = 5;
            const int THRESHOLD = 300;

            var greatestOffset = 0d;

            foreach (var part in parts)
            {
                var lastEdgeTime = double.NegativeInfinity;

                foreach (var phrase in part.NotePhrases)
                {
                    foreach (var lyric in phrase.Lyrics)
                    {
                        if (lyric.PitchSlide)
                        {
                            continue;
                        }

                        if (lyric.Time < lastEdgeTime)
                        {
                            // This lyric is too early to be spawned right on cue, and will have to be offset.
                            // Check if the offset is the biggest we've seen so far
                            greatestOffset = Math.Max(greatestOffset, lastEdgeTime - lyric.Time);
                        }
                        var spawnTime = Math.Max(lyric.Time, lastEdgeTime);

                        textWidthTester.text = lyric.Text;
                        var width = textWidthTester.GetPreferredValues().x;

                        lastEdgeTime = spawnTime + (width + VocalLyricContainer.LYRIC_SPACING) / DEFAULT_TRACK_SPEED;
                    }
                }
            }

            if (greatestOffset < THRESHOLD)
            {
                return 1f;
            }

            // Every 200 units past the threshold increases the scaling factor (plus an initial increase for
            // passing the threshold in the first place)
            int severity = (((int)greatestOffset - THRESHOLD) / 200) + 1;

            return 1f + (severity * 0.3f);
        }

        // Necessary for combining HARM2 and HARM3 in two-lane view
        #nullable enable
        public struct VocalPhrasePair
        {
            public double Tick;
            public double Time;

            // In three-lane view, this is always populated
            // In two-lane view, the HARM2 tracker might have some VocalPhrasePairs where this is null but mergedPhrase is not (for phrases that include
            // HARM3 but not HARM2). Still always populated for HARM1
            public VocalsPhrase? MainPhrase;

            // In three-lane view, this is always null
            // In two-lane view, this is populated with HARM3's phrases. When HARM2 and HARM3 share a phrase, both fields are populated. Still always null
            // for HARM1
            public VocalsPhrase? MergedPhrase;

            public VocalPhrasePair(VocalsPhrase? mainPhrase, VocalsPhrase? mergedPhrase)
            {
                MainPhrase = mainPhrase;
                MergedPhrase = mergedPhrase;

                if (mainPhrase is not null)
                {
                    Tick = mainPhrase.Tick;
                    Time = mainPhrase.Time;
                } else if (mergedPhrase is not null)
                {
                    Tick = mergedPhrase.Tick;
                    Time = mergedPhrase.Time;
                } else
                {
                    throw new InvalidOperationException("Tried to create VocalPhrasePair with two null phrases");
                }
            }

            // Percussion is only valid on Solo Vocals and HARM1, so the merged phrase can be assumed false
            public readonly bool IsPercussion => MainPhrase?.IsPercussion ?? false;

            public readonly bool IsStarPower => MainPhrase?.IsStarPower ?? MergedPhrase!.IsStarPower;

            public double Duration => GetLastNoteTotalEndTime() - GetFirstNoteStartTime();

            public double GetFirstNoteStartTime()
            {
                if (MergedPhrase is null)
                {
                    return MainPhrase!.PhraseParentNote.Time;
                }
                if (MainPhrase is null)
                {
                    return MergedPhrase.PhraseParentNote.Time;
                } else
                {
                    return Math.Min(MainPhrase.PhraseParentNote.Time, MergedPhrase.PhraseParentNote.Time);
                }
            }

            public double GetLastNoteTotalEndTime()
            {
                if (MergedPhrase is null)
                {
                    return MainPhrase!.PhraseParentNote.ChildNotes[^1].TotalTimeEnd;
                }
                if (MainPhrase is null)
                {
                    return MergedPhrase.PhraseParentNote.ChildNotes[^1].TotalTimeEnd;
                }
                else
                {
                    return Math.Max(MainPhrase.PhraseParentNote.ChildNotes[^1].TotalTimeEnd, MergedPhrase.PhraseParentNote.ChildNotes[^1].TotalTimeEnd);
                }
            }
        }

        private List<VocalPhrasePair> GetVocalPhrasePairs(VocalsPart mainPart, VocalsPart? mergedPart)
        {
            var phrasePairs = new List<VocalPhrasePair>();

            if (mergedPart is null)
            {
                foreach (var phrase in mainPart.StaticLyricPhrases)
                {
                    phrasePairs.Add(new(phrase, null));
                }
            }
            else
            {
                var mergedPhraseIdx = 0;

                foreach (var mainPhrase in mainPart.StaticLyricPhrases)
                {
                    // Capture any HARM3-only phrases that happened since last time
                    while (mergedPhraseIdx < mergedPart.StaticLyricPhrases.Count && mergedPart.StaticLyricPhrases[mergedPhraseIdx].Tick < mainPhrase.Tick)
                    {
                        phrasePairs.Add(new(null, mergedPart.StaticLyricPhrases[mergedPhraseIdx++]));
                    }

                    // Capture HARM2+3 phrase
                    if (mergedPhraseIdx < mergedPart.StaticLyricPhrases.Count && mergedPart.StaticLyricPhrases[mergedPhraseIdx].Tick == mainPhrase.Tick)
                    {
                        phrasePairs.Add(new(mainPhrase, mergedPart.StaticLyricPhrases[mergedPhraseIdx++]));
                    }

                    // Capture HARM2-only phrase
                    else
                    {
                        phrasePairs.Add(new(mainPhrase, null));
                    }
                }

                // Capture any remaining HARM3-only phrases after the last HARM2 phrase
                while (mergedPhraseIdx < mergedPart.StaticLyricPhrases.Count)
                {
                    phrasePairs.Add(new(null, mergedPart.StaticLyricPhrases[mergedPhraseIdx++]));
                }
            }

            return phrasePairs;
        }
        #nullable disable
    }
}

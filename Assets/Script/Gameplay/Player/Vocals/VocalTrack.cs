using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Gameplay.HUD;
using YARG.Core.Logging;
using YARG.Gameplay.Visuals;
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

        public void InitializeRenderTexture(float vocalImageAspectRatio, RenderTexture renderTexture)
        {
            // Set the vocal track render texture to a constant aspect ratio
            // to make it easier to work with and size.
            // int height = (int) (Screen.width / vocalImageAspectRatio);
            float height =  Screen.width / vocalImageAspectRatio / Screen.height;
            _trackCamera.rect = new Rect(0.0f, 1.0f - height, 1.0f, height);

            // Apply the render texture
            _trackCamera.targetTexture = renderTexture;
        }

        public void Initialize(VocalsTrack vocalsTrack, YargPlayer primaryPlayer)
        {
            _originalVocalsTrack = vocalsTrack;

            // Apply the modifiers of the primary player. All players should have the
            // same modifier(s) chosen.
            foreach (var part in _originalVocalsTrack.Parts)
            {
                primaryPlayer.Profile.ApplyVocalModifiers(part);
            }

            _vocalsTrack = _originalVocalsTrack.Clone();
            TrackSpeed = primaryPlayer.Profile.NoteSpeed;
            _lyricContainer.TrackSpeed = TrackSpeed;

            // Create trackers and indices
            var parts = _vocalsTrack.Parts;
            _phraseMarkerIndices = new int[parts.Count];
            _noteTrackers = new PhraseNoteTracker[parts.Count];
            _lyricTrackers = new PhraseNoteTracker[parts.Count];

            // Create PhraseNoteTrackers
            for (int i = 0; i < parts.Count; i++)
            {
                _noteTrackers[i] = new PhraseNoteTracker(parts[i], false);
                _lyricTrackers[i] = new PhraseNoteTracker(parts[i], true);
            }

            // Choose the correct amount of lanes
            LyricLaneCount = 1;
            if (vocalsTrack.Instrument == Instrument.Harmony)
            {
                LyricLaneCount = SettingsManager.Settings.UseThreeLaneLyricsInHarmony.Value
                    ? 3
                    : 2;
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

        public void UpdateCountdown(int measuresLeft, double countdownLength, double endTime)
        {
            if (_countdownDisplay == null)
            {
                return;
            }

            _countdownDisplay.UpdateCountdown(measuresLeft, countdownLength, endTime);
        }

        private void Update()
        {
            double time = GameManager.RealVisualTime;

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
            for (int i = 0; i < _noteTrackers.Length; i++)
            {
                _phraseMarkerIndices[i] = 0;
                _noteTrackers[i].Reset();
                _lyricTrackers[i].Reset();
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

                _noteTrackers[i] = new PhraseNoteTracker(part, false);
                _lyricTrackers[i] = new PhraseNoteTracker(part, true);
            }

            // The most recent range shift before the start tick should still be preserved
            uint rangesStart = _vocalsTrack.RangeShifts.LowerBoundElement(start).Tick;
            _vocalsTrack.RangeShifts.RemoveAll(n => n.Tick < rangesStart || n.Tick >= end);

            ResetPracticeSection();
        }
    }
}

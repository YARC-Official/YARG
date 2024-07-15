using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Gameplay.Visuals;
using YARG.Player;

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
        }

        private static readonly int _alphaMultiplier = Shader.PropertyToID("AlphaMultiplier");

        // TODO: Temporary until color profiles for vocals
        public readonly Color[] Colors =
        {
            new(0f, 0.800f, 1f, 1f),
            new(1f, 0.522f, 0f, 1f),
            new(1f, 0.859f, 0f, 1f)
        };

        // Time offset relative to 1.0 note speed
        private const float SPAWN_TIME_OFFSET = 25f;

        public float SpawnTimeOffset => SPAWN_TIME_OFFSET / TrackSpeed;

        private const float TRACK_TOP = 0.90f;
        private const float TRACK_TOP_HARMONY = 0.53f;

        private const float TRACK_BOTTOM = -0.53f;

        private const float NOTE_WIDTH_MULTIPLIER = 1.5f;

        private const float MINIMUM_SEMITONE_RANGE = 20;

        private const double MINIMUM_SHIFT_TIME = 0.25;

        [SerializeField]
        private GameObject _vocalPlayerPrefab;

        [Space]
        [SerializeField]
        private MeshRenderer _trackRenderer;
        [SerializeField]
        private Material _twoLaneHarmonyTrackMaterial;
        [SerializeField]
        private Material _threeLaneHarmonyTrackMaterial;

        [Space]
        [SerializeField]
        private MeshRenderer _soloStarpowerOverlay;
        [SerializeField]
        private MeshRenderer _harmonyStarpowerOverlay;

        [Space]
        [SerializeField]
        private Camera _trackCamera;
        [SerializeField]
        private Transform _playerContainer;
        [SerializeField]
        private Pool[] _notePools;
        [SerializeField]
        private Pool _talkiePool;
        [SerializeField]
        private VocalLyricContainer _lyricContainer;
        [SerializeField]
        private Pool _phraseLinePool;

        private readonly List<VocalsPlayer> _vocalPlayers = new();
        private bool _currentStarpowerState;

        private float _currentTrackTop = TRACK_TOP;
        private Material _starpowerMaterial;

        private VocalsTrack _originalVocalsTrack;
        private VocalsTrack _vocalsTrack;

        private bool _isRangeChanging;
        private Range _viewRange;
        private Range _targetRange;
        private Range _previousRange;

        private int _nextRangeIndex = 1;
        private double _changeStartTime;
        private double _changeEndTime;

        public float TrackSpeed { get; private set; }

        public bool HarmonyShowing => _vocalsTrack.Instrument == Instrument.Harmony;

        public float CurrentNoteWidth =>
            ((_currentTrackTop - TRACK_BOTTOM) / (_viewRange.Max - _viewRange.Min)) * NOTE_WIDTH_MULTIPLIER;

        private void Start()
        {
            Assert.AreEqual(_notePools.Length, 3,
                "Note pools must be of length three (one for each harmony part).");
        }

        public RenderTexture InitializeRenderTexture(float vocalImageAspectRatio)
        {
            // Set the vocal track render texture to a constant aspect ratio
            // to make it easier to work with and size.
            int height = (int) (Screen.width / vocalImageAspectRatio);

            // Create a render texture for the vocals
            var descriptor = new RenderTextureDescriptor(
                Screen.width, height, RenderTextureFormat.ARGBHalf);
            descriptor.mipCount = 0;
            var renderTexture = new RenderTexture(descriptor);

            // Apply the render texture
            _trackCamera.targetTexture = renderTexture;

            return renderTexture;
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

            if (vocalsTrack.Instrument == Instrument.Harmony)
            {
                // Set the track material to harmony, if it's harmony (it's solo by default)
                _trackRenderer.material = _threeLaneHarmonyTrackMaterial;
                _currentTrackTop = TRACK_TOP_HARMONY;

                // Show the correct starpower overlay
                _soloStarpowerOverlay.gameObject.SetActive(false);
                _harmonyStarpowerOverlay.gameObject.SetActive(true);
                _starpowerMaterial = _harmonyStarpowerOverlay.material;
            }
            else
            {
                // Show the correct starpower overlay
                _harmonyStarpowerOverlay.gameObject.SetActive(false);
                _soloStarpowerOverlay.gameObject.SetActive(true);
                _starpowerMaterial = _soloStarpowerOverlay.material;
            }

            // Set pitch range
            ChangeRange(_vocalsTrack.RangeShifts[0]);
            _viewRange = _targetRange;
            _previousRange = _targetRange;
            _changeEndTime = _changeStartTime;

            // Hide overlay
            _starpowerMaterial.SetFloat(_alphaMultiplier, 0f);
        }

        public VocalsPlayer CreatePlayer()
        {
            var playerObj = Instantiate(_vocalPlayerPrefab, _playerContainer);
            var player = playerObj.GetComponent<VocalsPlayer>();

            _vocalPlayers.Add(player);

            return player;
        }

        private void Update()
        {
            double time = GameManager.RealVisualTime;

            // Handle range changes
            var ranges = _vocalsTrack.RangeShifts;
            while (_nextRangeIndex < ranges.Count && ranges[_nextRangeIndex].Time < time)
            {
                ChangeRange(ranges[_nextRangeIndex]);
                _nextRangeIndex++;
            }

            // Update the range
            if (_isRangeChanging)
            {
                float changePercent = (float) YargMath.InverseLerpD(_changeStartTime, _changeEndTime, time);

                // If the change has finished, stop!
                if (changePercent >= 1f)
                {
                    _isRangeChanging = false;
                    _viewRange.Min = _targetRange.Min;
                    _viewRange.Max = _targetRange.Max;
                }
                else
                {
                    float newMin = Mathf.Lerp(_previousRange.Min, _targetRange.Min, changePercent);
                    float newMax = Mathf.Lerp(_previousRange.Max, _targetRange.Max, changePercent);

                    _viewRange.Min = newMin;
                    _viewRange.Max = newMax;
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

        private void ChangeRange(VocalsRangeShift range)
        {
            // Pad out range based on note width
            float minPitch = range.MinimumPitch - NOTE_WIDTH_MULTIPLIER / 2;
            float maxPitch = range.MaximumPitch + NOTE_WIDTH_MULTIPLIER / 2;

            // Ensure range is at least a minimum size
            float rangeMiddle = (range.MaximumPitch + range.MinimumPitch) / 2;
            float rangeMin = Math.Min(rangeMiddle - (MINIMUM_SEMITONE_RANGE / 2), minPitch);
            float rangeMax = Math.Max(rangeMiddle + (MINIMUM_SEMITONE_RANGE / 2), maxPitch);

            // Start the change!
            _previousRange = _viewRange;
            _targetRange = new Range(rangeMin, rangeMax);

            _changeStartTime = range.Time;
            _changeEndTime = range.Time + Math.Max(MINIMUM_SHIFT_TIME, range.TimeLength);
            _isRangeChanging = true;
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
        }

        public void SetPracticeSection(uint start, uint end)
        {
            // Skip if no vocals
            if (!gameObject.activeSelf) return;

            _vocalsTrack = _originalVocalsTrack.Clone();

            // Remove all notes not in the section
            for (int i = 0; i < _vocalsTrack.Parts.Count; i++)
            {
                var part = _vocalsTrack.Parts[i];
                part.NotePhrases.RemoveAll(n => n.Tick < start || n.Tick >= end);
                part.TextEvents.RemoveAll(n => n.Tick < start || n.Tick >= end);

                _noteTrackers[i] = new PhraseNoteTracker(part, false);
                _lyricTrackers[i] = new PhraseNoteTracker(part, true);
            }

            ResetPracticeSection();
        }
    }
}
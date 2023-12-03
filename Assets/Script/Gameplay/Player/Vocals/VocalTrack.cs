using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;
using YARG.Core;
using YARG.Core.Chart;

namespace YARG.Gameplay.Player
{
    public partial class VocalTrack : GameplayBehaviour
    {
        private struct Range
        {
            // These are basically just random numbers
            // public static readonly Range Default = new(55f, 75f);
            // TODO: Make the default range very large until proper range shifting is implemented
            public static readonly Range Default = new(40f, 87f);

            public float Min;
            public float Max;

            public Range(float min, float max)
            {
                Min = min;
                Max = max;
            }
        }

        private static readonly int _alphaMultiplier = Shader.PropertyToID("AlphaMultiplier");

        // TODO: This is temporary
        public const float NOTE_SPEED = 5f;

        // TODO: Temporary until color profiles for vocals
        public readonly Color[] Colors =
        {
            new(0f, 0.800f, 1f, 1f),
            new(1f, 0.522f, 0f, 1f),
            new(1f, 0.859f, 0f, 1f)
        };

        private const float SPAWN_TIME_OFFSET = 5f;

        private const float TRACK_TOP = 0.90f;
        private const float TRACK_TOP_HARMONY = 0.53f;

        private const float TRACK_BOTTOM = -0.53f;

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

        public bool IsRangeChanging { get; private set; }

        private VocalsTrack _originalVocalsTrack;
        private VocalsTrack _vocalsTrack;

        private Range _viewRange = Range.Default;
        private Range _targetRange;
        private Range _changeSpeed;
        private float _changeTimer;

        public bool HarmonyShowing => _vocalsTrack.Instrument == Instrument.Harmony;

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

        public void Initialize(VocalsTrack vocalsTrack)
        {
            _originalVocalsTrack = vocalsTrack;
            _vocalsTrack = _originalVocalsTrack.Clone();

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
            // Update the range
            if (IsRangeChanging)
            {
                float newMin = _viewRange.Min + _changeSpeed.Min * Time.deltaTime;
                float newMax = _viewRange.Max + _changeSpeed.Max * Time.deltaTime;

                _viewRange.Min = newMin;
                _viewRange.Max = newMax;

                _changeTimer -= Time.deltaTime;

                // If the change has finished, stop!
                if (_changeTimer <= 0f)
                {
                    IsRangeChanging = false;
                    _viewRange.Min = _targetRange.Min;
                    _viewRange.Max = _targetRange.Max;
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

        private void CalculateAndChangeRange(double noteRangeStart, double noteRangeEnd, float changeTime)
        {
            if (IsRangeChanging) return;

            // Get the min and max range
            _targetRange = new Range(float.MaxValue, float.MinValue);
            foreach (var part in _vocalsTrack.Parts)
            {
                foreach (var note in part.NotePhrases.SelectMany(i => i.PhraseParentNote.ChildNotes))
                {
                    // If the note time is less than the range start,
                    // skip until we're in the range.
                    if (note.TotalTimeEnd < noteRangeStart) continue;

                    // If the note time is more than the range end,
                    // we're done.
                    if (note.Time > noteRangeEnd) break;

                    // Get the lowest and highest pitch in the note
                    float lowest = note.ChordEnumerator().Min(i => i.Pitch);
                    float highest = note.ChordEnumerator().Max(i => i.Pitch);

                    // Set the new target range values
                    if (lowest < _targetRange.Min) _targetRange.Min = lowest;
                    if (highest > _targetRange.Max) _targetRange.Max = highest;
                }
            }

            // If there are no notes in the range, then just use the default range
            if (float.IsInfinity(_targetRange.Max) || float.IsInfinity(_targetRange.Min))
            {
                _targetRange = Range.Default;
            }

            // Get speed from the change time
            _changeSpeed = new Range(
                (_targetRange.Min - _viewRange.Min) / changeTime,
                (_targetRange.Max - _viewRange.Max) / changeTime);

            // Start the change!
            _changeTimer = changeTime;
            IsRangeChanging = true;
        }

        public float GetPosForTime(double time)
        {
            return (float) time * NOTE_SPEED;
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
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Gameplay.Visuals;

namespace YARG.Gameplay.Player
{
    public class VocalTrack : GameplayBehaviour
    {
        private struct Range
        {
            // These are basically just random numbers
            public static readonly Range Default = new(55f, 75f);

            public float Min;
            public float Max;

            public Range(float min, float max)
            {
                Min = min;
                Max = max;
            }
        }

        // TODO: This is temporary
        public const float NOTE_SPEED = 5f;

        private const float SPAWN_TIME_OFFSET = 5f;

        private const float TRACK_TOP = 0.90f;
        private const float TRACK_BOTTOM = -0.53f;

        [SerializeField]
        private GameObject _vocalPlayerPrefab;

        [Space]
        [SerializeField]
        private Camera _trackCamera;
        [SerializeField]
        private Transform _playerContainer;
        [SerializeField]
        private Pool[] _notePools;
        [SerializeField]
        private Pool _phraseLinePool;

        public bool IsRangeChanging { get; private set; }

        private VocalsTrack _vocalsTrack;
        private int[] _phraseIndices;
        private int[] _noteIndices;

        private Range _viewRange = Range.Default;
        private Range _targetRange;
        private Range _changeSpeed;
        private float _changeTimer;

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
            _vocalsTrack = vocalsTrack;
            _phraseIndices = new int[_vocalsTrack.Parts.Count];
            _noteIndices = new int[_vocalsTrack.Parts.Count];
        }

        public VocalsPlayer CreatePlayer()
        {
            var playerObj = Instantiate(_vocalPlayerPrefab, _playerContainer);
            var player = playerObj.GetComponent<VocalsPlayer>();
            return player;
        }

        private void Update()
        {
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                CalculateAndChangeRange(GameManager.SongTime, GameManager.SongTime + 10.0, 1f);
            }

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

            // For each harmony...
            for (int i = 0; i < _vocalsTrack.Parts.Count; i++)
            {
                // Get the information for this harmony part
                var phrases = _vocalsTrack.Parts[i].NotePhrases;
                int index = _phraseIndices[i];

                // Spawn the notes in the phrase.
                // We don't need to do a time check as
                // that is handled in SpawnNotesInPhrase.
                while (index < phrases.Count)
                {
                    var phrase = phrases[index];

                    if (SpawnNotesInPhrase(phrase, i))
                    {
                        // Spawn the phrase end line
                        var poolable = _phraseLinePool.TakeWithoutEnabling();
                        ((PhraseLineElement) poolable).PhraseRef = phrase;
                        poolable.EnableFromPool();

                        // Next phrase!
                        index++;
                        _noteIndices[i] = 0;
                    }
                    else
                    {
                        break;
                    }
                }

                // Make sure to update the value
                _phraseIndices[i] = index;
            }
        }

        private bool SpawnNotesInPhrase(VocalsPhrase phrase, int harmonyIndex)
        {
            var pool = _notePools[harmonyIndex];
            int index = _noteIndices[harmonyIndex];

            while (index < phrase.Notes.Count && phrase.Notes[index].Time <= GameManager.SongTime + SPAWN_TIME_OFFSET)
            {
                // Skip this frame if the pool is full
                if (!pool.CanSpawnAmount(1))
                {
                    return false;
                }

                // Spawn the vocal note
                var poolable = pool.TakeWithoutEnabling();
                ((VocalNoteElement) poolable).NoteRef = phrase.Notes[index];
                poolable.EnableFromPool();

                index++;
            }

            // Make sure to update the value
            _noteIndices[harmonyIndex] = index;

            return index >= phrase.Notes.Count;
        }

        private void CalculateAndChangeRange(double noteRangeStart, double noteRangeEnd, float changeTime)
        {
            if (IsRangeChanging) return;

            // Get the min and max range
            _targetRange = new Range(float.MaxValue, float.MinValue);
            foreach (var part in _vocalsTrack.Parts)
            {
                foreach (var note in part.NotePhrases.SelectMany(i => i.Notes))
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
            return YargMath.Lerp(TRACK_BOTTOM, TRACK_TOP, _viewRange.Min, _viewRange.Max, pitch);
        }
    }
}
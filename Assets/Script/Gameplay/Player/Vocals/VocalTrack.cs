﻿using System.Linq;
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

        // TODO: Temporary until color profiles for vocals
        public readonly Color[] Colors =
        {
            new(0f, 0.800f, 1f, 1f),
            new(1f, 0.522f, 0f, 1f),
            new(1f, 0.859f, 0f, 1f)
        };

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
        private Pool _talkiePool;
        [SerializeField]
        private VocalLyricContainer _lyricContainer;
        [SerializeField]
        private Pool _phraseLinePool;

        public bool IsRangeChanging { get; private set; }

        private VocalsTrack _vocalsTrack;
        private int[] _phraseIndices;
        private int[] _noteIndices;
        private int[] _lyricIndices;

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
            _lyricIndices = new int[_vocalsTrack.Parts.Count];
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

                    var notesSpawned = SpawnNotesInPhrase(phrase, i);
                    var lyricsSpawned = SpawnLyricsInPhrase(phrase, i);
                    if (notesSpawned && lyricsSpawned)
                    {
                        // Spawn the phrase end line
                        var poolable = _phraseLinePool.TakeWithoutEnabling();
                        ((PhraseLineElement) poolable).PhraseRef = phrase;
                        poolable.EnableFromPool();

                        // Next phrase!
                        index++;
                        _noteIndices[i] = 0;
                        _lyricIndices[i] = 0;
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
            var notes = phrase.PhraseParentNote.ChildNotes;

            while (index < notes.Count && notes[index].Time <= GameManager.SongTime + SPAWN_TIME_OFFSET)
            {
                var note = notes[index];

                if (note.IsNonPitched)
                {
                    // Skip this frame if the pool is full
                    if (!_talkiePool.CanSpawnAmount(1))
                    {
                        return false;
                    }

                    // Spawn the vocal note
                    var noteObj = _talkiePool.TakeWithoutEnabling();
                    ((VocalTalkieElement) noteObj).NoteRef = note;
                    noteObj.EnableFromPool();
                }
                else
                {
                    // Skip this frame if the pool is full
                    if (!pool.CanSpawnAmount(1))
                    {
                        return false;
                    }

                    // Spawn the vocal note
                    var noteObj = pool.TakeWithoutEnabling();
                    ((VocalNoteElement) noteObj).NoteRef = note;
                    noteObj.EnableFromPool();
                }

                index++;
            }

            // Make sure to update the value
            _noteIndices[harmonyIndex] = index;

            return index >= notes.Count;
        }

        private bool SpawnLyricsInPhrase(VocalsPhrase phrase, int harmonyIndex)
        {
            int index = _lyricIndices[harmonyIndex];
            var lyrics = phrase.Lyrics;
            var notes = phrase.PhraseParentNote.ChildNotes;

            while (index < lyrics.Count && lyrics[index].Time <= GameManager.SongTime + SPAWN_TIME_OFFSET)
            {
                var lyric = lyrics[index];

                // Get the probable note pair (for length and starpower)
                VocalNote probableNote = null;
                foreach (var note in notes)
                {
                    if (note.Tick != lyric.Tick) continue;

                    probableNote = note;
                }

                if (!_lyricContainer.TrySpawnLyric(lyric, probableNote, phrase.IsStarPower, harmonyIndex))
                {
                    return false;
                }

                index++;
            }

            // Make sure to update the value
            _lyricIndices[harmonyIndex] = index;

            return index >= lyrics.Count;
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
            var lerp = YargMath.Lerp(TRACK_BOTTOM, TRACK_TOP, _viewRange.Min, _viewRange.Max, pitch);
            return Mathf.Clamp(lerp, TRACK_BOTTOM, TRACK_TOP);
        }
    }
}
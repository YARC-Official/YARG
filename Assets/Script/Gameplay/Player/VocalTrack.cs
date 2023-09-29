using UnityEngine;
using UnityEngine.Assertions;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Gameplay.Visuals;

namespace YARG.Gameplay.Player
{
    public class VocalTrack : GameplayBehaviour
    {
        // TODO: This is temporary
        public const float NOTE_SPEED = 5f;

        private const float SPAWN_TIME_OFFSET = 5f;

        private const float TRACK_TOP = 0.95f;
        private const float TRACK_BOTTOM = -0.57f;

        [SerializeField]
        private Camera _trackCamera;
        [SerializeField]
        private Pool[] _notePools;

        private VocalsTrack _vocalsTrack;
        private int[] _phraseIndices;
        private int[] _noteIndices;

        // Set a starting range (these are basically just random numbers)
        private float _viewRangeMin = 55f;
        private float _viewRangeMax = 75f;

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

        private void Update()
        {
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

        public float GetPosForTime(double time)
        {
            return (float) time * NOTE_SPEED;
        }

        public float GetPosForPitch(float pitch)
        {
            return YargMath.Lerp(TRACK_BOTTOM, TRACK_TOP, _viewRangeMin, _viewRangeMax, pitch);
        }
    }
}
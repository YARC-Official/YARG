using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Game;
using YARG.Gameplay;
using YARG.Gameplay.Player;
using YARG.Gameplay.Visuals;

namespace YARG.Settings
{
    public class FakeTrackPlayer : MonoBehaviour
    {
        public const float NOTE_SPEED = 6f;
        private const double SPAWN_FREQ = 0.2;

        private double SpawnTimeOffset => (BasePlayer.SPAWN_OFFSET + -BasePlayer.STRIKE_LINE_POS) / NOTE_SPEED;

        [SerializeField]
        private TrackMaterial _trackMaterial;
        [SerializeField]
        private FretArray _fretArray;
        [SerializeField]
        private KeyedPool _notePool;

        public double PreviewTime { get; private set; }
        private double _nextSpawnTime;

        private void Start()
        {
            _fretArray.Initialize(ColorProfile.Default, false);
        }

        private void Update()
        {
            PreviewTime += Time.deltaTime;

            // Queue the notes
            if (_nextSpawnTime <= PreviewTime)
            {
                // Create a fake note. Ticks do not matter.
                var note = new GuitarNote(
                    Random.Range(1, 6),
                    GuitarNoteType.Strum,
                    GuitarNoteFlags.None,
                    NoteFlags.None,
                    PreviewTime + SpawnTimeOffset,
                    0, 0, 0);

                // Create note every N seconds
                _nextSpawnTime = PreviewTime + SPAWN_FREQ;

                // Spawn note
                var noteObj = (FakeNote) _notePool.KeyedTakeWithoutEnabling(note);
                noteObj.NoteRef = note;
                noteObj.FakeTrackPlayer = this;
                noteObj.EnableFromPool();
            }

            _trackMaterial.SetTrackScroll(PreviewTime, NOTE_SPEED);
        }
    }
}
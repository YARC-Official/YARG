using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Venue
{
    public sealed class ProGuitarChannel : IVenueChannel
    {
        private readonly Animator         _animator;
        private readonly VenueHashLibrary _hashes;
        private readonly int              _leadingFrames;

        public ProGuitarChannel(Animator animator, VenueHashLibrary hashes, int leadingFrames)
        {
            _animator = animator;
            _hashes = hashes;
            _leadingFrames = leadingFrames;
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            var proGuitar22 = chart.ProGuitar_22Fret.GetDifficulty(Difficulty.Expert);
            var proGuitarNoteList = proGuitar22.Notes;
            if (proGuitarNoteList.Count == 0)
            {
                var proGuitar17 = chart.ProGuitar_17Fret.GetDifficulty(Difficulty.Expert);
                proGuitarNoteList = proGuitar17.Notes;
            }

            foreach (var pgNote in proGuitarNoteList)
            {
                double t = pgNote.Time - _leadingFrames / 60.0;
                foreach (var note in pgNote.AllNotes)
                {
                    int hash = _hashes.ProGuitarHashes[note.String * VenueHashLibrary.FretCount + note.Fret];
                    var length = Mathf.Max((float) pgNote.TimeLength, 0.0167f);
                    queue.Add(AnimatorCommand.Randomize(t, _animator));
                    queue.Add(AnimatorCommand.BoolOn(t, _animator, hash, length));
                }
            }
        }

        public void Update(double visualTime)
        {
        }

        public void Initialize(EngineManager manager)
        {
        }
    }
}
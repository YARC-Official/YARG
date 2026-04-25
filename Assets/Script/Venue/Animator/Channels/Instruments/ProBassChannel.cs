using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Venue
{
    public sealed class ProBassChannel : IVenueChannel
    {
        private readonly Animator         _animator;
        private readonly VenueHashLibrary _hashes;
        private readonly int              _leadingFrames;

        public ProBassChannel(Animator animator, VenueHashLibrary hashes, int leadingFrames)
        {
            _animator = animator;
            _hashes = hashes;
            _leadingFrames = leadingFrames;
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            var proBass22 = chart.ProBass_22Fret.GetDifficulty(Difficulty.Expert);
            var proBassNoteList = proBass22.Notes;
            if (proBassNoteList.Count == 0)
            {
                var proBass17 = chart.ProBass_17Fret.GetDifficulty(Difficulty.Expert);
                proBassNoteList = proBass17.Notes;
            }

            foreach (var pbNote in proBassNoteList)
            {
                double t = pbNote.Time - _leadingFrames / 60.0;
                foreach (var note in pbNote.AllNotes)
                {
                    int hash = _hashes.ProBassHashes[note.String * VenueHashLibrary.FretCount + note.Fret];
                    var length = Mathf.Max((float) pbNote.TimeLength, 0.0167f);
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
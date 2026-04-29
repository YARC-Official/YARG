using System;
using System.Collections;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Venue
{
    public sealed class GuitarChannel : IVenueChannel
    {
        private readonly Animator         _animator;
        private readonly VenueHashLibrary _hashes;
        private readonly int              _leadingFrames;

        public GuitarChannel(Animator animator, VenueHashLibrary hashes, int leadingFrames)
        {
            _animator = animator;
            _hashes = hashes;
            _leadingFrames = leadingFrames;
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            var guitarId = chart.FiveFretGuitar.GetDifficulty(Difficulty.Expert);
            foreach (var gNote in guitarId.Notes)
            {
                double t = gNote.Time - _leadingFrames / 60.0;
                byte[] gNoteByte = BitConverter.GetBytes(gNote.NoteMask);
                BitArray gNoteMask = new BitArray(gNoteByte);

                for (int i = 0; i < 7 && i < gNoteMask.Length; i++)
                {
                    if (gNoteMask[i])
                    {
                        // Turn the note off after a frame unless it is longer
                        var length = Mathf.Max((float) gNote.TimeLength, 0.0167f);
                        queue.Add(AnimatorCommand.Randomize(t, _animator));
                        queue.Add(AnimatorCommand.BoolOn(t, _animator, _hashes.GuitarNoteHashes[i], length));
                    }
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
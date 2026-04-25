using System;
using System.Collections;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Venue
{
    public sealed class BassChannel : IVenueChannel
    {
        private readonly Animator         _animator;
        private readonly VenueHashLibrary _hashes;
        private readonly int              _leadingFrames;

        public BassChannel(Animator animator, VenueHashLibrary hashes, int leadingFrames)
        {
            _animator = animator;
            _hashes = hashes;
            _leadingFrames = leadingFrames;
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            var bassId = chart.FiveFretBass.GetDifficulty(Difficulty.Expert);
            foreach (var bNote in bassId.Notes)
            {
                double t = bNote.Time - _leadingFrames / 60.0;
                byte[] bNoteByte = BitConverter.GetBytes(bNote.NoteMask);
                BitArray bNoteMask = new BitArray(bNoteByte);

                for (int i = 0; i < 7 && i < bNoteMask.Length; i++)
                {
                    if (bNoteMask[i])
                    {
                        var length = Mathf.Max((float) bNote.TimeLength, 0.0167f);
                        queue.Add(AnimatorCommand.Randomize(t, _animator));
                        queue.Add(AnimatorCommand.BoolOn(t, _animator, _hashes.BassNoteHashes[i], length));
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
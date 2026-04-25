using System;
using System.Collections;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Venue
{
    public sealed class KeysChannel : IVenueChannel
    {
        private readonly Animator         _animator;
        private readonly VenueHashLibrary _hashes;
        private readonly int              _leadingFrames;

        public KeysChannel(Animator animator, VenueHashLibrary hashes, int leadingFrames)
        {
            _animator = animator;
            _hashes = hashes;
            _leadingFrames = leadingFrames;
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            var keysId = chart.Keys.GetDifficulty(Difficulty.Expert);
            foreach (var kNote in keysId.Notes)
            {
                double t = kNote.Time - _leadingFrames / 60.0;
                byte[] kNoteByte = BitConverter.GetBytes(kNote.NoteMask);
                BitArray kNoteMask = new BitArray(kNoteByte);

                for (int i = 0; i < 5 && i < kNoteMask.Length; i++)
                {
                    if (kNoteMask[i])
                    {
                        var length = Mathf.Max((float) kNote.TimeLength, 0.0167f);
                        queue.Add(AnimatorCommand.Randomize(t, _animator));
                        queue.Add(AnimatorCommand.BoolOn(t, _animator, _hashes.KeysNoteHashes[i], length));
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
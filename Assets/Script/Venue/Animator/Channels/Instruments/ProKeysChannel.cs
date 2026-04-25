using System;
using System.Collections;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Venue
{
    public sealed class ProKeysChannel : IVenueChannel
    {
        private readonly Animator         _animator;
        private readonly VenueHashLibrary _hashes;
        private readonly int              _leadingFrames;

        public ProKeysChannel(Animator animator, VenueHashLibrary hashes, int leadingFrames)
        {
            _animator = animator;
            _hashes = hashes;
            _leadingFrames = leadingFrames;
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            var proKeysId = chart.ProKeys.GetDifficulty(Difficulty.Expert);
            foreach (var pkNote in proKeysId.Notes)
            {
                double t = pkNote.Time - _leadingFrames / 60.0;
                byte[] pkNoteByte = BitConverter.GetBytes(pkNote.NoteMask);
                BitArray pkNoteMask = new BitArray(pkNoteByte);

                for (int i = 0; i < 25 && i < pkNoteMask.Length; i++)
                {
                    if (pkNoteMask[i])
                    {
                        var length = Mathf.Max((float) pkNote.TimeLength, 0.0167f);
                        queue.Add(AnimatorCommand.Randomize(t, _animator));
                        queue.Add(AnimatorCommand.BoolOn(t, _animator, _hashes.ProKeysHashes[i], length));
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
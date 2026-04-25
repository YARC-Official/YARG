using System;
using System.Collections;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Venue
{
    public sealed class DrumChannel : IVenueChannel
    {
        private readonly Animator         _animator;
        private readonly VenueHashLibrary _hashes;
        private readonly int              _leadingFrames;

        public DrumChannel(Animator animator, VenueHashLibrary hashes, int leadingFrames)
        {
            _animator = animator;
            _hashes = hashes;
            _leadingFrames = leadingFrames;
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            var drumsId = chart.ProDrums.GetDifficulty(Difficulty.Expert);
            if (drumsId.Notes.Count == 0)
            {
                drumsId = chart.FourLaneDrums.GetDifficulty(Difficulty.Expert);
            }

            foreach (var dNote in drumsId.Notes)
            {
                double t = dNote.Time - _leadingFrames / 60.0;
                int dPads = 0;
                foreach (var drum in dNote.AllNotes)
                {
                    dPads |= (1 << drum.Pad);
                }
                byte[] dNoteByte = BitConverter.GetBytes(dPads);
                BitArray dNoteMask = new BitArray(dNoteByte);

                for (int i = 0; i < 8 && i < dNoteMask.Length; i++)
                {
                    if (dNoteMask[i])
                    {
                        queue.Add(AnimatorCommand.Randomize(t, _animator));
                        queue.Add(AnimatorCommand.Trigger(t, _animator, _hashes.DrumNoteHashes[i]));
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
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Venue
{
    public sealed class BeatlineChannel : IVenueChannel
    {
        private readonly Animator         _animator;
        private readonly VenueHashLibrary _hashes;
        private readonly int              _leadingFrames;

        public BeatlineChannel(Animator animator, VenueHashLibrary hashes, int leadingFrames)
        {
            _animator = animator;
            _hashes = hashes;
            _leadingFrames = leadingFrames;
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            foreach (var beat in chart.SyncTrack.Beatlines)
            {
                double t = beat.Time - _leadingFrames / 60.0;
                int hash = _hashes.BeatlineHashes[(int) beat.Type];
                queue.Add(AnimatorCommand.Randomize(t, _animator));
                queue.Add(AnimatorCommand.Trigger(t, _animator, hash));
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
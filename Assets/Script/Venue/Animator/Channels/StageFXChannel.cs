using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Venue
{
    public sealed class StageFXChannel : IVenueChannel
    {
        private readonly Animator         _animator;
        private readonly VenueHashLibrary _hashes;
        private readonly int              _leadingFrames;

        public StageFXChannel(Animator animator, VenueHashLibrary hashes, int leadingFrames)
        {
            _animator = animator;
            _hashes = hashes;
            _leadingFrames = leadingFrames;
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            var events = chart.VenueTrack.Stage;
            foreach (var e in events)
            {
                double t = e.Time - _leadingFrames / 60.0;
                switch (e.Effect)
                {
                    case StageEffect.FogOn:
                        queue.Add(AnimatorCommand.Randomize(t, _animator));
                        queue.Add(AnimatorCommand.BoolOn(t, _animator, _hashes.Fog, 0f));
                        break;
                    case StageEffect.FogOff:
                        queue.Add(AnimatorCommand.BoolOff(t, _animator, _hashes.Fog));
                        break;
                    case StageEffect.BonusFx:
                        queue.Add(AnimatorCommand.Randomize(t, _animator));
                        queue.Add(AnimatorCommand.Trigger(t, _animator, _hashes.BonusFx));
                        break;
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
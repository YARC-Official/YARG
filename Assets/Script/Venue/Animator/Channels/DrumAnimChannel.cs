using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;
using AnimationEvent = YARG.Core.Chart.AnimationEvent;

namespace YARG.Venue
{
    public sealed class DrumAnimChannel : IVenueChannel
    {
        private readonly Animator         _animator;
        private readonly VenueHashLibrary _hashes;
        private readonly int              _leadingFrames;

        public DrumAnimChannel(Animator animator, VenueHashLibrary hashes, int leadingFrames)
        {
            _animator = animator;
            _hashes = hashes;
            _leadingFrames = leadingFrames;
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            var drumsTrack = chart.GetDrumsTrack(Instrument.ProDrums);
            if (drumsTrack.Animations.AnimationEvents.Count == 0)
            {
                drumsTrack = chart.GetDrumsTrack(Instrument.FourLaneDrums);
            }

            foreach (var dAnim in drumsTrack.Animations.AnimationEvents)
            {
                double t = dAnim.Time - _leadingFrames / 60.0;

                queue.Add(AnimatorCommand.Randomize(t, _animator));

                int hash = _hashes.DrumAnimHashes[(int) dAnim.Type];

                if (dAnim.Type == AnimationEvent.AnimationType.OpenHiHat)
                {
                   queue.Add(AnimatorCommand.BoolOn(t, _animator, hash, (float) dAnim.TimeLength));
                }
                else
                {
                    queue.Add(AnimatorCommand.Trigger(t, _animator, hash));
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
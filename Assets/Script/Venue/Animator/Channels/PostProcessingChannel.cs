using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Venue
{
    public sealed class PostProcessingChannel : IVenueChannel
    {
        private readonly Animator         _animator;
        private readonly VenueHashLibrary _hashes;
        private readonly int              _leadingFrames;
        private readonly int              _postProcessingLayerHash;

        public PostProcessingChannel(Animator animator, VenueHashLibrary hashes, int leadingFrames)
        {
            _animator = animator;
            _hashes = hashes;
            _leadingFrames = leadingFrames;
            _postProcessingLayerHash = _animator.GetLayerIndex("PostProcessing");
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            var events = chart.VenueTrack.PostProcessing;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                double t = e.Time - _leadingFrames / 60.0;

                int hash = e.Type == PostProcessingType.Default
                    ? _hashes.PostProcessingBlendHashes[(int) PostProcessingType.Default]
                    : _hashes.PostProcessingBlendHashes[(int) e.Type];

                if (i + 1 < events.Count)
                {
                    var next = events[i + 1];
                    int nextHash = next.Type == PostProcessingType.Default
                        ? _hashes.PostProcessingBlendHashes[(int) PostProcessingType.Default]
                        : _hashes.PostProcessingBlendHashes[(int) next.Type];

                    if (hash == nextHash)
                    {
                        float duration = (float) (next.Time - e.Time);
                        queue.Add(AnimatorCommand.Blend(t, _animator, nextHash, duration, _postProcessingLayerHash));
                        continue;
                    }
                }

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
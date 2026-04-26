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
            _postProcessingLayerHash = hashes.PostProcessingLayerHash;
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            var events = chart.VenueTrack.PostProcessing;
            bool suppressNext = false;

            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                double t = e.Time - _leadingFrames / 60.0;

                // Always re-roll RNG before update
                queue.Add(AnimatorCommand.Randomize(t, _animator));

                int hash = e.Type == PostProcessingType.Default
                    ? _hashes.PPBlendDefault
                    : _hashes.PostProcessingBlendHashes[(int) e.Type];

                int unblendedHash = e.Type == PostProcessingType.Default
                    ? _hashes.PPDefault
                    : _hashes.PostProcessingHashes[(int) e.Type];

                int prevBlendHash = -1;
                if (i > 0)
                {
                    var prev = events[i - 1];
                    prevBlendHash = prev.Type == PostProcessingType.Default
                        ? _hashes.PPBlendDefault
                        : _hashes.PostProcessingBlendHashes[(int) prev.Type];
                }

                // Handle crossfade
                if (prevBlendHash != -1 && hash == prevBlendHash && i + 1 < events.Count)
                {
                    var next = events[i + 1];
                    int nextHash = next.Type == PostProcessingType.Default
                        ? _hashes.PPBlendDefault
                        : _hashes.PostProcessingBlendHashes[(int) next.Type];

                    if (prevBlendHash == hash)
                    {
                        float duration = (float) (next.Time - e.Time);
                        queue.Add(AnimatorCommand.Blend(t, _animator, nextHash, duration, _postProcessingLayerHash));
                        suppressNext = true;
                        continue;
                    }
                }

                if (!suppressNext)
                {
                    queue.Add(AnimatorCommand.Trigger(t, _animator, unblendedHash));
                }
                suppressNext = false;
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
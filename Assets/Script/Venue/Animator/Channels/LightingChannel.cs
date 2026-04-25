using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Venue
{
    public sealed class LightingChannel : IVenueChannel
    {
        private readonly Animator         _animator;
        private readonly VenueHashLibrary _hashes;
        private readonly int              _leadingFrames;
        private readonly int              _lightingLayerHash;

        public LightingChannel(Animator animator, VenueHashLibrary hashes, int leadingFrames)
        {
            _animator = animator;
            _hashes = hashes;
            _leadingFrames = leadingFrames;
            _lightingLayerHash = _animator.GetLayerIndex("Lighting");
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            var events = chart.VenueTrack.Lighting;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                double t = e.Time - _leadingFrames / 60.0;

                // Resolve the hash without any string allocation
                int hash = e.Type is LightingType.Default or LightingType.Intro
                    ? _hashes.LightingBlendHashes[(int) LightingType.Default]
                    : _hashes.LightingBlendHashes[(int) e.Type];

                // Crossfade if same event repeats and there is a known next event
                if (i + 1 < events.Count)
                {
                    var next = events[i + 1];
                    int nextHash = next.Type is LightingType.Default or LightingType.Intro
                        ? _hashes.LightingBlendHashes[(int) LightingType.Default]
                        : _hashes.LightingBlendHashes[(int) next.Type];

                    if (hash == nextHash)
                    {
                        float duration = (float) (next.Time - e.Time);
                        queue.Add(AnimatorCommand.Blend(t, _animator, nextHash, duration, _lightingLayerHash));
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
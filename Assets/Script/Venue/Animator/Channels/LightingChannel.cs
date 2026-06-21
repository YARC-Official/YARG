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
            _lightingLayerHash = hashes.LightingLayerHash;
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            var events = chart.VenueTrack.Lighting;
            bool suppressNext = false;

            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                double t = e.Time - _leadingFrames / 60.0;

                // Always re-roll RNG before update
                queue.Add(AnimatorCommand.Randomize(t, _animator));

                // Resolve the hash without any string allocation
                int blendHash = e.Type is LightingType.Default or LightingType.Intro
                    ? _hashes.LightBlendDefault
                    : _hashes.LightingBlendHashes[(int) e.Type];

                // We also need the unblended hash for SetTrigger
                int unblendedHash = e.Type is LightingType.Default or LightingType.Intro
                    ? _hashes.LightDefault
                    : _hashes.LightingHashes[(int) e.Type];

                // Determine previous hash
                int prevBlendHash = -1;
                if (i > 0)
                {
                    var prev = events[i - 1];
                    prevBlendHash = prev.Type is LightingType.Default or LightingType.Intro
                        ? _hashes.LightingBlendHashes[(int) LightingType.Default]
                        : _hashes.LightingBlendHashes[(int) prev.Type];
                    if (prev.Type is LightingType.KeyframeFirst or LightingType.KeyframeNext
                        or LightingType.KeyframePrevious)
                    {
                        prevBlendHash = -1;
                    }
                }

                int nexti = i + 1;

                while (nexti < events.Count && (events[nexti].Type is LightingType.KeyframeFirst
                    or LightingType.KeyframeNext or LightingType.KeyframePrevious))
                {
                    nexti++;
                }

                // Crossfade if same event repeats and there is a known next event
                if (blendHash == prevBlendHash && blendHash != -1 && nexti < events.Count)
                {
                    var next = events[nexti];
                    int nextHash = next.Type is LightingType.Default or LightingType.Intro
                        ? _hashes.LightingBlendHashes[(int) LightingType.Default]
                        : _hashes.LightingBlendHashes[(int) next.Type];

                    float duration = (float) (next.Time - e.Time);
                    queue.Add(AnimatorCommand.Blend(t, _animator, nextHash, duration, _lightingLayerHash));
                    suppressNext = true;
                    continue;
                }

                // Last event of certain types is handled differently
                if (i == events.Count - 1 && e.Type is LightingType.Frenzy
                    or LightingType.CoolAutomatic or LightingType.WarmAutomatic)
                {
                    queue.Add(AnimatorCommand.Float(t, _animator, _hashes.BPMAdjust, 0f));
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
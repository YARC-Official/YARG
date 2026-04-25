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
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                double t = e.Time - _leadingFrames / 60.0;

                // Always re-roll RNG before update
                queue.Add(AnimatorCommand.Randomize(t, _animator));

                // Resolve the hash without any string allocation
                int hash = e.Type is LightingType.Default or LightingType.Intro
                    ? _hashes.LightDefault
                    : _hashes.LightingBlendHashes[(int) e.Type];

                int nexti = i + 1;

                while (nexti < events.Count && (events[nexti].Type is LightingType.KeyframeFirst
                    or LightingType.KeyframeNext or LightingType.KeyframePrevious))
                {
                    nexti++;
                }

                // Crossfade if same event repeats and there is a known next event
                if (nexti < events.Count && events[i].Type == events[nexti].Type && e.Type
                    is not LightingType.KeyframeFirst and not LightingType.KeyframeNext
                    and not LightingType.KeyframePrevious)
                {
                    var next = events[nexti];
                    int nextHash = next.Type is LightingType.Default or LightingType.Intro
                        ? _hashes.LightDefault
                        : _hashes.LightingBlendHashes[(int) next.Type];

                    float duration = (float) (next.Time - e.Time);
                    queue.Add(AnimatorCommand.Blend(t, _animator, nextHash, duration, _lightingLayerHash));
                    continue;
                }

                // Last event of certain types is handled differently
                if (i == events.Count - 1 && e.Type is LightingType.Frenzy
                    or LightingType.CoolAutomatic or LightingType.WarmAutomatic)
                {
                    queue.Add(AnimatorCommand.Float(t, _animator, _hashes.BPMAdjust, 0f));
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
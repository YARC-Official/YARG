using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;

namespace YARG.Venue
{
    public sealed class HappinessChannel : IVenueChannel
    {
        private readonly Animator         _animator;
        private readonly VenueHashLibrary _hashes;

        private float _prevHappiness = -1f;
        private int   _prevHappHash;

        private EngineManager _engineManager;

        public HappinessChannel(Animator animator, VenueHashLibrary hashes, EngineManager engineManager)
        {
            _animator = animator;
            _hashes = hashes;
            _engineManager = engineManager;
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
        }

        public void Update(double visualTime)
        {
            float happiness = _engineManager.Happiness;
            if (happiness == _prevHappiness)
            {
                return;
            }

            _animator.SafeSetFloat(_hashes.Happiness, happiness);

            int newHash = happiness > 0.666f ? _hashes.HappyHigh
                : happiness < 0.333f         ? _hashes.HappyLow
                                               : _hashes.HappyMed;

            if (newHash != _prevHappHash)
            {
                _hashes.Randomize(_animator);
                _animator.SafeSetBool(newHash, true);
                if (_prevHappHash != 0) _animator.SafeSetBool(_prevHappHash, false);
                _prevHappHash = newHash;
            }

            _prevHappiness = happiness;
        }
    }
}
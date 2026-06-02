using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Parsing;

namespace YARG.Venue
{
    public sealed class CrowdChannel : IVenueChannel
    {
        private readonly Animator         _animator;
        private readonly VenueHashLibrary _hashes;
        private readonly int              _leadingFrames;

        private EngineManager    _engineManager;
        private List<CrowdEvent> _events;
        private int              _eventIndex;

        private int  _crowdLimit;
        private bool _crowdClapOff;
        private int  _prevCrowdState = -1;
        private bool _prevClap;

        public CrowdChannel(Animator animator, VenueHashLibrary hashes, int leadingFrames, EngineManager engineManager)
        {
            _animator = animator;
            _hashes = hashes;
            _leadingFrames = leadingFrames;
            _engineManager = engineManager;
        }

        public void BuildCommands(SongChart chart, AnimatorCommandQueue queue)
        {
            _events = chart.CrowdEvents;
            _eventIndex = 0;
            _crowdLimit = 0;
            _crowdClapOff = false;
        }

        public void Update(double visualTime)
        {
            if (_events == null) return;

            while (_eventIndex < _events.Count && _events[_eventIndex].Time - _leadingFrames / 60.0 <= visualTime)
            {
                var crowd = _events[_eventIndex];
                switch (crowd.CrowdState)
                {
                    case CrowdState.Realtime: _crowdLimit = 0; break;
                    case CrowdState.Mellow:   _crowdLimit = 1; break;
                    case CrowdState.Normal:   _crowdLimit = 2; break;
                    case CrowdState.Intense:  _crowdLimit = 3; break;
                }

                switch (crowd.ClapState)
                {
                    case ClapState.NoClap:
                        _crowdClapOff = true;
                        _hashes.Randomize(_animator);
                        _animator.SafeSetBool(_hashes.CrowdClap, false);
                        break;
                    case ClapState.Clap:
                        _crowdClapOff = false;
                        if (_engineManager.Happiness >= 1f)
                        {
                            _hashes.Randomize(_animator);
                            _animator.SafeSetBool(_hashes.CrowdClap, true);
                        }
                        break;
                }
                _eventIndex++;
            }

            float happiness = _engineManager.Happiness;
            int crowdHappiness = happiness > 0.666f ? 3 : happiness > 0.333f ? 2 : 1;
            bool crowdClap = happiness >= 1f;

            int crowdState = Math.Min(_crowdLimit, crowdHappiness);
            if (crowdState != _prevCrowdState)
            {
                _hashes.Randomize(_animator);
                _animator.SafeSetTrigger(_hashes.CrowdStateHashes[crowdState]);
                _prevCrowdState = crowdState;
            }

            if (!_crowdClapOff && crowdClap != _prevClap)
            {
                _hashes.Randomize(_animator);
                _animator.SafeSetBool(_hashes.CrowdClap, crowdClap);
                _prevClap = crowdClap;
            }
        }
    }
}
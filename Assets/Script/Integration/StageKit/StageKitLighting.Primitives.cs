using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlasticBand.Haptics;
using YARG.Core.Chart;
using YARG.Gameplay;
using YARG.Playback;
using Object = UnityEngine.Object;

namespace YARG.Integration.StageKit
{
    public class BeatPattern : StageKitLighting
    {
        private readonly bool _continuous;
        private int _patternIndex;
        private readonly (StageKitLedColor color, byte data)[] _patternList;
        private GameManager _gameManager;
        private readonly float _beatsPerCycle;

        public BeatPattern((StageKitLedColor, byte)[] patternList, float beatsPerCycle, bool continuous = true)
        {
            _continuous = continuous;
            _patternList = patternList;
            _beatsPerCycle = beatsPerCycle;
        }

        public override void Enable()
        {
            _patternIndex = 0;
            // Brought to you by Hacky Hack and the Hacktones
            _gameManager = Object.FindObjectOfType<GameManager>();
            _gameManager.BeatEventHandler.Visual.Subscribe(OnBeat, BeatEventType.DenominatorBeat, division: _beatsPerCycle / _patternList.Length);
        }

        private void OnBeat()
        {
            StageKitInterpreter.Instance.SetLed(_patternList[_patternIndex].color, _patternList[_patternIndex].data);
            _patternIndex++;

            // Some beat patterns are not continuous (single fire), so we need to kill them after they've run once
            // otherwise they pile up.
            if (!_continuous && _patternIndex == _patternList.Length)
            {
                _gameManager.BeatEventHandler.Visual.Unsubscribe(OnBeat);
                KillSelf();
            }

            if (_patternIndex >= _patternList.Length)
            {
                _patternIndex = 0;
            }
        }

        public override void KillSelf()
        {
            if (_gameManager != null)
            {
                _gameManager.BeatEventHandler.Visual.Unsubscribe(OnBeat);
            }
        }
    }

    public class ListenPattern : StageKitLighting
    {
        private readonly ListenTypes _listenType;
        private int _patternIndex;
        private readonly (StageKitLedColor color, byte data)[] _patternList;
        private readonly bool _flash;
        private readonly bool _inverse;
        private bool _enabled;

        public ListenPattern((StageKitLedColor color, byte data)[] patternList, ListenTypes listenType,
            bool flash = false, bool inverse = false)
        {
            _flash = flash;
            _patternList = patternList;
            _listenType = listenType;
            _inverse = inverse;
        }

        public override void Enable()
        {
            _patternIndex = 0;
            _enabled = true;
            if (!_inverse) return;
            StageKitInterpreter.Instance.SetLed(_patternList[_patternIndex].color, _patternList[_patternIndex].data);
            _patternIndex++;

            if (_patternIndex >= _patternList.Length)
            {
                _patternIndex = 0;
            }
        }

        public override void HandleBeatlineEvent(BeatlineType eventName)
        {
            if (!_enabled)
            {
                return;
            }

            if (((_listenType & ListenTypes.MajorBeat) == 0 || eventName != BeatlineType.Measure) &&
                ((_listenType & ListenTypes.MinorBeat) == 0 || eventName != BeatlineType.Strong))
            {
                return;
            }

            ProcessEvent();
        }

        public override void HandleDrumEvent(int eventName)
        {
            if (!_enabled)
            {
                return;
            }

            if ((_listenType & ListenTypes.RedFretDrums) == 0 || (eventName & (int)FourLaneDrumPad.RedDrum) == 0)
            {
                return;
            }

            ProcessEvent();
        }

        public override void HandleLightingEvent(LightingType eventName)
        {
            if (!_enabled)
            {
                return;
            }

            if ((_listenType & ListenTypes.Next) == 0 || eventName != LightingType.KeyframeNext)
            {
                return;
            }

            ProcessEvent();
        }

        private void ProcessEvent()
        {
            // This might be a bug in the official stage kit code. Instead of turning off the strobe as soon as cue
            // changes, if the cue listens for something, it only turns off the strobe on the first event of it.
            // To make that happen, strobe off would have to be here and removed from the master controller as well as
            // added to the lighting event switch case for all the non-listening cues.

            if (_inverse)
            {
                StageKitInterpreter.Instance.SetLed(_patternList[_patternIndex].color, NONE);
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(_patternList[_patternIndex].color,
                    _patternList[_patternIndex].data);
            }

            if (_flash)
            {
                OnFlash().Forget();
            }

            _patternIndex++;

            if (_patternIndex >= _patternList.Length)
            {
                _patternIndex = 0;
            }
        }

        private async UniTaskVoid OnFlash()
        {
            // I wonder if this should be beat based instead of time based. like 1/2 a beat or something.
            // But a really fast song would be bad looking.
            await UniTask.Delay(200);
            if (_inverse)
            {
                StageKitInterpreter.Instance.SetLed(_patternList[_patternIndex].color,
                    _patternList[_patternIndex].data);
            }
            else
            {
                StageKitInterpreter.Instance.SetLed(_patternList[_patternIndex].color, NONE);
            }
        }

        public override void KillSelf()
        {
            _enabled = false;
        }
    }

    public class TimedPattern : StageKitLighting
    {
        private readonly float _seconds;
        private int _patternIndex;
        private readonly (StageKitLedColor color, byte data)[] _patternList;
        private CancellationTokenSource _cancellationTokenSource;

        public TimedPattern((StageKitLedColor, byte)[] patternList, float seconds)
        {
            // Token only for timed events
            _cancellationTokenSource = new CancellationTokenSource();
            _seconds = seconds;
            _patternList = patternList;
        }

        public override void Enable()
        {
            _patternIndex = 0;
            _cancellationTokenSource = new CancellationTokenSource();
            TimedCircleCoroutine(_cancellationTokenSource.Token).Forget();
        }

        private async UniTask TimedCircleCoroutine(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                StageKitInterpreter.Instance.SetLed(_patternList[_patternIndex].color,
                    _patternList[_patternIndex].data);

                await UniTask.Delay(TimeSpan.FromSeconds(_seconds / _patternList.Length),
                    cancellationToken: cancellationToken);

                _patternIndex++;

                if (_patternIndex >= _patternList.Length)
                {
                    _patternIndex = 0;
                }
            }
        }

        public override void KillSelf()
        {
            _cancellationTokenSource?.Cancel();
        }
    }
}

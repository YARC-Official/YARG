using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlasticBand.Haptics;
using YARG.Core.Chart;
using YARG.Gameplay;
using Object = UnityEngine.Object;

namespace YARG.Integration.StageKit
{
    public class BeatPattern : StageKitLighting
    {
        private readonly bool _continuous;
        private int _patternIndex = 0;
        private readonly (int color, byte data)[] _patternList;
        private GameManager _gameManager;

        public BeatPattern((int, byte)[] patternList, bool continuous = true, float timesPerBeat = 1.0f)
        {
            //Brought to you by Hacky Hack and the Hacktones
            _gameManager = Object.FindObjectOfType<GameManager>();
            _continuous = continuous;
            _patternList = patternList;
            _gameManager.BeatEventManager.Subscribe(OnBeat,
                new BeatEventManager.Info(1.0f / (timesPerBeat * _patternList.Length), 0f));
        }

        public override void OnBeat()
        {
            StageKitLightingController.Instance.SetLed(_patternList[_patternIndex].color,
                _patternList[_patternIndex].data);
            _patternIndex++;

            //some beat patterns are not continuous (single fire), so we need to kill them after
            //they've run once otherwise they pile up.
            if (!_continuous && _patternIndex == _patternList.Length)
            {
                _gameManager.BeatEventManager.Unsubscribe(OnBeat);
                StageKitLightingController.Instance.CurrentLightingCue.CuePrimitives.Remove(this);
            }

            if (_patternIndex >= _patternList.Length)
            {
                _patternIndex = 0;
            }
        }
    }

    public class ListenPattern : StageKitLighting
    {
        private readonly ListenTypes _listenType;
        private int _patternIndex;
        private readonly (int color, byte data)[] _patternList;
        private readonly bool _flash;
        private readonly bool _inverse;

        public ListenPattern((int color, byte data)[] patternList, ListenTypes listenType, bool flash = false,
            bool inverse = false)
        {
            _flash = flash;
            _patternList = patternList;
            _listenType = listenType;
            _inverse = inverse;

            if (!_inverse) return;
            StageKitLightingController.Instance.SetLed(_patternList[_patternIndex].color,
                _patternList[_patternIndex].data);
            _patternIndex++;

            if (_patternIndex >= _patternList.Length)
            {
                _patternIndex = 0;
            }
        }

        public override void HandleBeatlineEvent(BeatlineType eventName)
        {
            if (((_listenType & ListenTypes.MajorBeat) == 0 || eventName != BeatlineType.Measure) &&
                ((_listenType & ListenTypes.MinorBeat) == 0 || eventName != BeatlineType.Strong))
            {
                return;
            }

            ProcessEvent();
        }

        public override void HandleDrumEvent(int eventName)
        {
            if ((_listenType & ListenTypes.RedFretDrums) == 0 || eventName != (int) FourLaneDrumPad.RedDrum)
            {
                return;
            }

            ProcessEvent();
        }

        public override void HandleLightingEvent(LightingType eventName)
        {
            if ((_listenType & ListenTypes.Next) == 0 || eventName != LightingType.Keyframe_Next)
            {
                return;
            }

            ProcessEvent();
        }

        private void ProcessEvent()
        {
            //This might be a bug in the official  stagekit code i'm trying to replicate here, but instead of turning off the strobe as soon as cue changes, if the cue listens for something, it only turns off the strobe on the first event of it.
            StageKitLightingController.Instance.SetStrobeSpeed(StageKitStrobeSpeed.Off);

            if (_inverse)
            {
                StageKitLightingController.Instance.SetLed(_patternList[_patternIndex].color, NONE);
            }
            else
            {
                StageKitLightingController.Instance.SetLed(_patternList[_patternIndex].color,
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
                StageKitLightingController.Instance.SetLed(_patternList[_patternIndex].color,
                    _patternList[_patternIndex].data);
            }
            else
            {
                StageKitLightingController.Instance.SetLed(_patternList[_patternIndex].color, NONE);
            }
        }
    }

    public class TimedPattern : StageKitLighting
    {
        private readonly float _seconds;
        private int _patternIndex;
        private readonly (int color, byte data)[] _patternList;

        public TimedPattern((int, byte)[] patternList, float seconds)
        {
            //Token only for timed events
            CancellationTokenSource = new CancellationTokenSource();
            _seconds = seconds;
            _patternList = patternList;
            TimedCircleCoroutine(CancellationTokenSource.Token).Forget();
        }

        private async UniTask TimedCircleCoroutine(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                StageKitLightingController.Instance.SetLed(_patternList[_patternIndex].color,
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
    }
}
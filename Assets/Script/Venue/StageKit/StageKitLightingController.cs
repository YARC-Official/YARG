using System;
using System.Collections.Generic;
using UnityEngine;
using PlasticBand.Haptics;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;

namespace YARG
{
    public class StageKitLightingController : MonoBehaviour
    {
        public enum StrobeSpeed
        {
            Off,
            Slow,
            Medium,
            Fast,
            Fastest
        }

        public enum FogState
        {
            Off = 0,
            On = 1,
        }

        public static StageKitLightingController Instance { get; private set; }

        public FogState currentFogState = FogState.Off;
        public FogState previousFogState = FogState.Off;

        public StrobeSpeed currentStrobeState = StrobeSpeed.Off;
        public StrobeSpeed previousStrobeState = StrobeSpeed.Off;

        public List<IStageKitHaptics> StageKits = new();

        public StageKitLightingCues CurrentLightingCue = null;

        // Stuff for the actual command sending to the unit
        private bool _isSendingCommands;
        private readonly Queue<(int, byte)> _commandQueue = new();
        private byte[] _currentLedState = { 0x00, 0x00, 0x00, 0x00 };             //blue, green, yellow, red
        private byte[] _previousLedState = { 0x00, 0x00, 0x00, 0x00 }; //this is only for the SendCommands() command to limit swamping the kit.
        private readonly float _sendDelay = 0.001f;                              //necessary to prevent the stage kit from getting overwhelmed and dropping commands. In seconds. 0.001 is the minimum. Preliminary testing indicated that 7ms was needed to prevent dropped commands, but it seems that most songs are slow enough to allow 1ms.

        private void OnDeviceChange(InputDevice device, InputDeviceChange change) // Listen for new stage kits being added or removed at any time.
        {
            if (change == InputDeviceChange.Added)
            {
                if (device is IStageKitHaptics haptics) StageKits.Add(haptics);
            }
            else if (change == InputDeviceChange.Removed)
            {
                if (device is IStageKitHaptics haptics) StageKits.Remove(haptics);
            }
        }

        private void Start()
        {
            Instance = this;

            foreach (var device in InputSystem.devices) //build a list of all the stage kits connected
            {
                if (device is IStageKitHaptics haptics) StageKits.Add(haptics);
            }

            InputSystem.onDeviceChange += OnDeviceChange; //then listen to see if any more are added or removed
            StageKits.ForEach(kit => kit.ResetHaptics()); //StageKits remember its last state which is neat but not needed on startup
        }
        private void OnApplicationQuit()
        {
            StageKits.ForEach(kit => kit.ResetHaptics()); //turn off everything when the game closes
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        //The actual queueing and sending of commands
        private void EnqueueCommand(int color, byte ledByte)
        {
            _commandQueue.Enqueue((color, ledByte));

            if (_isSendingCommands)
            {
                return;
            }

            SendCommands().Forget();
        }

        private async UniTask SendCommands()
        {
            _isSendingCommands = true;
            var things = CurrentLightingCue;
            while (_commandQueue.Count > 0)
            {
                var curCommand = _commandQueue.Dequeue();

                switch (curCommand.Item1)
                {
                    case 0:
                    case 1:
                    case 2:
                    case 3:
                        if (_currentLedState[curCommand.Item1] == _previousLedState[curCommand.Item1])
                        {
                            await UniTask.Yield();
                        }

                        var iToStageKitLedColor = curCommand.Item1 switch
                        {
                            0 => StageKitLedColor.Blue,
                            1 => StageKitLedColor.Green,
                            2 => StageKitLedColor.Yellow,
                            3 => StageKitLedColor.Red,
                            _ => StageKitLedColor.All
                        };
                        StageKits.ForEach(kit => kit.SetLeds(iToStageKitLedColor, (StageKitLed)curCommand.Item2)); //This is where the magic happens
                        _previousLedState[curCommand.Item1] = _currentLedState[curCommand.Item1];
                        break;
                    case 4:
                        StageKits.ForEach(kit => kit.SetFogMachine(curCommand.Item2 == 1));
                        break;
                    case 5:
                        StageKits.ForEach(kit => kit.SetStrobeSpeed((StageKitStrobeSpeed)curCommand.Item2));
                        break;
                    default:
                        Debug.Log("Unknown command: " + curCommand.Item1);
                        break;
                }

                if (things != CurrentLightingCue && _commandQueue.Count > (0.05f / _sendDelay) ) //If there is more 1/20th of a second in commands left in the queue when the cue changes, clear it. Really fast songs can build up a queue in the thousands while in BRE or Frenzy. 1/20th of a second is said to be the blink of an eye.
                {
                    _commandQueue.Clear();
                    things = CurrentLightingCue;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(_sendDelay));
            }

            _isSendingCommands = false;
        }

        public void SetLed(int color, byte led)
        {
            _currentLedState[color] = led;
            EnqueueCommand(color, _currentLedState[color]);
        }

        public void SetFogMachine(FogState fogState)
        {
            if (currentFogState == fogState)
            {
                return;
            }
            EnqueueCommand(4, (byte)fogState  );

            currentFogState = fogState;
        }

        public void SetStrobeSpeed(StrobeSpeed strobeSpeed)
        {
            if (currentStrobeState == strobeSpeed)
            {
                return;
            }

            switch (strobeSpeed)
            {
                case StrobeSpeed.Off:
                    EnqueueCommand(5, (byte)StageKitStrobeSpeed.Off);
                    break;

                case StrobeSpeed.Slow:
                    EnqueueCommand(5, (byte)StageKitStrobeSpeed.Slow);
                    break;

                case StrobeSpeed.Medium:
                    EnqueueCommand(5, (byte)StageKitStrobeSpeed.Medium);
                    break;

                case StrobeSpeed.Fast:
                    CurrentLightingCue?.Dispose(true);
                    EnqueueCommand(5, (byte)StageKitStrobeSpeed.Fast);
                    break;

                case StrobeSpeed.Fastest:
                    EnqueueCommand(5, (byte)StageKitStrobeSpeed.Fastest);
                    break;

                default:
                    Debug.LogWarning("Unknown strobe speed.");
                    break;
            }
            currentStrobeState = strobeSpeed;
        }
    }
}
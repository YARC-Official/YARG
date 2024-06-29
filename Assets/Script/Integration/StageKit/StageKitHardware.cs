using System;
using System.Collections.Generic;
using UnityEngine;
using PlasticBand.Haptics;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using YARG.Core.Logging;

/*
 Software Layout:
    Cues are made up of set of primitives that either follow the beat, triggered by an event, or timed. Some cues have
    multiple patterns. These patterns are not random, the same song in the same venue will have the same patterns for
    the cues each time. I do not know how that decided. Each venue is flagged as either Large or Small. This is used to
    determine which pattern sets will be uses for some cues. This is randomized currently, since there is no way to get
    venue size from the game, at the moment.

 Hardware layout:
    LED numbers on the pod:
    rockband logo
     7 0 1
      \|/
    6 -+-  2
      /|\
     5 4 3
    xbox button
    as well as the fog machine and strobe light. The fog machine has 2 settings, on and off. The strobe light has 5
    settings, off, slow, medium, fast, fastest. Only slow and fast are used in official songs. For details on how to
    send commands to the kit, see the IStageKitHaptics interface and Nate's fantastic work
    at https://github.com/TheNathannator/PlasticBand/blob/main/Docs/Other/Stage%20Kit/Xbox%20360.md

 Bugs and notes:
    During official light shows (For example, using the stage kit with RB2, on a xbox 360) the lights will sometimes
    behave in unexpected, song specific, one-off ways. It is hard to say if this is a bug or complex intended behavior.
    Since there are known bugs with the stage kit as is, I am going to assume these are also bugs since the programming
    required to make these bespoke song effects is not trivial and doesn't seem to make sense
    (such as random one led flashes). So stage shows will be slightly different than the official ones, but hopefully
    more consistent and predictable with what was intended. Sometimes just straight up better as entire cues are
    missing from the official shows.
    Sometimes pause doesn't work for fast strobe?? I legit don't know why this happens. It seems to be a
    bug with the stage kit itself.

  Not implemented because these things don't exist YARG:
    "About to fail song" light cues are not implemented since we don't support failing songs right now.
    Intro "walk on" lighting before the song starts (where it says "<user name> as <character name>") is not
    implemented because that doesn't exist.

 Implemented by YARG but not in the original game:
    Menu lighting


 The individual effects for the stage kit:

    NR = No visible response from Stage Kit
    VR = Response changes based on venue size
    RE = Response is the same regardless of venue size

    Keyframed calls:

        NR	verse						Doesn't seem to do anything.
        NR	chorus						Doesn't seem to do anything.

        RE	loop_cool                   2 blue LEDs 180 degrees apart rotating counter clockwise, 1 green led starting
                                        at 90 degrees rotating clockwise. To the beat.
        RE	loop_warm                   2 red LEDs 180 degrees apart rotating clockwise, 1 yellow led starting at 90
                                        counter rotating counterclockwise. To the beat.
        RE	manual_cool					2 blue LEDs 180 degrees apart rotating counter clockwise, 1 green led starting
                                        at 90 degrees rotating clockwise. To the beat. Does not turn off strobe on
                                        initial call, turns it off on [next]
        RE	manual_warm					2 red LEDs 180 degrees apart rotating clockwise, 1 yellow led starting at
                                        90 counter rotating counterclockwise. To the beat. Does not turn off strobe
                                        on initial call, turns it off on [next]

        VR	dischord					1 yellow led clock circles on major and minor beat.
                                        Red ring on drum red fret
                                        Blue follows [next], pattern is 6|2 ,off, 6|2|0|4. Turns off on initial call
                                        then on with next. (not 100% sure on this one)

                                        Small venue:
                                        1 Green led @ 0, counter-clockwise circles to beat.

                                        Large Venue:
                                        On Major beat toggles between: 1 Green led@0 counter clock circles to beat | all
                                        green leds on.

        VR	stomp						Initial call turns on leds. Responds to the [next], toggling lights on or off.

                                        Small Venue:
                                        Red, Green, Yellow

                                        Large Venue:
                                        All colors


        VR	Empty (i.e. [lighting ()])	Small Venue:
            Default lighting.			All red on, all blue on, changing on [next]. Yellow ring on, half beat flash on
                                        drum red fret.
                                        Green, off

                                        Large Venue:
                                        All blue on,  all red on. changing on [next].

    Automatic calls:

        RE	harmony             blue @4 clockwise on beat and green @4 clockwise on beat

        VR	frenzy				sequence of alternating patterns. I think each color is half a beat.
                                    Large venue: all red, off, all blue, all yellow
                                    Small venue: half red, other half red, 4 green , 2 side blue, other 6 blue

        RE	silhouettes			Turn on a green ring (doesn't seem to turn it off)

        NR	silhouettes_spot	Responses change depending on the cue before it.
                                For Dischord, It turns on both blue and green rings, with the blue toggling on and off
                                depending on the vocal note end after each major beat.
                                Does nothing with Stomp
                                Turns off all lights for everything else.

        VR	searchlights     	On beat.
                                Small venue pattern:
                                    1 yellow and 1 red rotate together. Counter clock

                                Large venue patterns:
                                    1 yellow@2 clockwise and 1 blue@0 counter clock.

                                    There are other patterns for both cues that change which leds start. I don't know
                                    how they are chosen yet.

        RE	sweep               On beat.
                                small venue:
                                    Yellow@ 6 and 2, counter-clock
                                    Blue@0, clock

                                Large venue:
                                    Red @6 and 2, counter-clock

        RE	strobe_slow			Strobe light that blinks every 16th note/120 ticks.
        RE	strobe_fast			Strobe light that blinks every 32nd note/60 ticks.
                                The strobe_off call is exceedingly rare, the strobe is typically turned off by other
                                cues starting.

        NR	blackout_fast		Turns off strobe. Turns of all LEDs
        NR	blackout_slow       Turns off strobe. Turns of all LEDs
        ??	blackout_spot       (untested, rare) Turns off strobe. Turns of all LEDs
        RE	flare_slow          All LEDS on.
        RE	flare_fast			All 8 blue LEDS turn on. Turns on greens after cool.
        RE	bre                 All Red, Green, Yellow, Blue leds turn on in sequence. Two times a beat.
        NR	bonusfx             Doesn't seem to do anything.
        NR	bonusfx_optional    Doesn't seem to do anything.

        RE	FogOn				Turns the fog machine on. No other cue interacts with the fog machine!
        RE	FogOff				Turns the fog machine off. No other cue interacts with the fog machine!

        RE	intro               Doesn't seem to do anything.

    Extra calls:
        VR	Score card  		small venue
                                2 yellow at 180 to each other clock 2 second starting on 6 and 2, 1 blue@0 counter
                                clock 1 second

                                large venue
                                2 yellow at 180 to each other clock 2 second, 2 red at 180 counter clock 1 second

        RE Menu lighting        1 blue@0 rotates counter clock every two seconds. Made by me. Not in the original game.
 */
namespace YARG.Integration.StageKit
{
    public class StageKitHardware : MonoSingleton<StageKitHardware>
    {
        private enum CommandType
        {
            LedBlue,
            LedGreen,
            LedYellow,
            LedRed,
            FogMachine,
            StrobeSpeed,
        }

        private readonly List<IStageKitHaptics> _stageKits = new();

        // Stuff for the actual command sending to the unit
        private bool _isSendingCommands;
        private readonly Queue<(int command, byte data)> _commandQueue = new();
        private byte _currentBlueLedState;
        private byte _currentGreenLedState;
        private byte _currentYellowLedState;
        private byte _currentRedLedState;

        // This is only for the SendCommands() command to limit swamping the kit.
        private byte _previousBlueLedState;
        private byte _previousGreenLedState;
        private byte _previousYellowLedState;
        private byte _previousRedLedState;

        // Necessary to prevent the stage kit from getting overwhelmed and dropping commands. In seconds. 0.001 is the
        // minimum. Preliminary testing indicated that 7ms was needed to prevent dropped commands, but it seems that
        // most songs are slow enough to allow 1ms.
        private const float SEND_DELAY = 0.001f;

        private void Start()
        {
            InputSystem.onDeviceChange += OnDeviceChange;

            // Build a list of all the stage kits connected
            foreach (var device in InputSystem.devices)
            {
                if (device is IStageKitHaptics haptics)
                {
                    _stageKits.Add(haptics);
                }
            }
        }

        protected override void SingletonDestroy()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;

            foreach (var kit in _stageKits)
            {
                kit.ResetHaptics();
            }
        }

        public void HandleEnabledChanged(bool isEnabled)
        {
            if (isEnabled)
            {
                // Stage Kits remember its last state which is neat but not needed on startup
                foreach (var kit in _stageKits)
                {
                    kit.ResetHaptics();
                }

                StageKitInterpreter.OnLedEvent += HandleLedEvent;
                StageKitInterpreter.OnFogMachineEvent += HandleFogEvent;
                StageKitInterpreter.OnStrobeSetEvent += HandleStrobeEvent;
            }
            else
            {
                StageKitInterpreter.OnLedEvent -= HandleLedEvent;
                StageKitInterpreter.OnFogMachineEvent -= HandleFogEvent;
                StageKitInterpreter.OnStrobeSetEvent -= HandleStrobeEvent;
            }
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            // Listen for new stage kits being added or removed at any time.
            if (change == InputDeviceChange.Added)
            {
                if (device is IStageKitHaptics haptics) _stageKits.Add(haptics);
            }
            else if (change == InputDeviceChange.Removed)
            {
                if (device is IStageKitHaptics haptics) _stageKits.Remove(haptics);
            }
        }

        //The actual queueing and sending of commands
        private void EnqueueCommand(int command, byte data)
        {
            _commandQueue.Enqueue((command, data));

            if (_isSendingCommands)
            {
                return;
            }

            SendCommands().Forget();
        }

        private async UniTask SendCommands()
        {
            _isSendingCommands = true;
            var things = MasterLightingController.CurrentLightingCue;

            while (_commandQueue.Count > 0)
            {
                var curCommand = _commandQueue.Dequeue();

                switch (curCommand.command)
                {
                    case (int) CommandType.LedBlue:
                        if (_currentBlueLedState == _previousBlueLedState)
                        {
                            await UniTask.Yield();
                        }

                        foreach (var kit in _stageKits)
                        {
                            kit.SetLeds(StageKitLedColor.Blue, (StageKitLed) curCommand.data);
                        }

                        _previousBlueLedState = _currentBlueLedState;
                        break;

                    case (int) CommandType.LedGreen:
                        if (_currentGreenLedState == _previousGreenLedState)
                        {
                            await UniTask.Yield();
                        }

                        foreach (var kit in _stageKits)
                        {
                            kit.SetLeds(StageKitLedColor.Green, (StageKitLed) curCommand.data);
                        }

                        _previousGreenLedState = _currentGreenLedState;
                        break;

                    case (int) CommandType.LedYellow:
                        if (_currentYellowLedState == _previousYellowLedState)
                        {
                            await UniTask.Yield();
                        }

                        foreach (var kit in _stageKits)
                        {
                            kit.SetLeds(StageKitLedColor.Yellow, (StageKitLed) curCommand.data);
                        }

                        _previousYellowLedState = _currentYellowLedState;
                        break;

                    case (int) CommandType.LedRed:
                        if (_currentRedLedState == _previousRedLedState)
                        {
                            await UniTask.Yield();
                        }

                        foreach (var kit in _stageKits)
                        {
                            kit.SetLeds(StageKitLedColor.Red, (StageKitLed) curCommand.data);
                        }

                        _previousRedLedState = _currentRedLedState;
                        break;

                    case (int) CommandType.FogMachine:
                        foreach (var kit in _stageKits)
                        {
                            kit.SetFogMachine(curCommand.data == 1);
                        }

                        break;

                    case (int) CommandType.StrobeSpeed:
                        foreach (var kit in _stageKits)
                        {
                            kit.SetStrobeSpeed((StageKitStrobeSpeed) curCommand.data);
                        }

                        break;

                    default:
                        YargLogger.LogWarning("Unknown command: " + curCommand.command);
                        break;
                }

                // If there is more 1/20th of a second in commands left in the queue when the cue changes, clear it.
                // Really fast songs can build up a queue in the thousands while in BRE or Frenzy. 1/20th of a
                // second is said to be the blink of an eye.
                if (things != MasterLightingController.CurrentLightingCue && _commandQueue.Count > 0.05f / SEND_DELAY)
                {
                    _commandQueue.Clear();
                    things = MasterLightingController.CurrentLightingCue;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(SEND_DELAY), ignoreTimeScale: true);
            }

            _isSendingCommands = false;
        }

        private void HandleFogEvent(MasterLightingController.FogState value)
        {
            EnqueueCommand((int) CommandType.FogMachine, (byte) value);
        }

        private void HandleStrobeEvent(StageKitStrobeSpeed value)
        {
            EnqueueCommand((int) CommandType.StrobeSpeed, (byte) value);
        }

        private void HandleLedEvent(StageKitLedColor color, byte led)
        {
            switch (color)
            {
                case StageKitLedColor.Blue:
                    _currentBlueLedState = led;
                    EnqueueCommand((int) CommandType.LedBlue, _currentBlueLedState);
                    break;

                case StageKitLedColor.Green:
                    _currentGreenLedState = led;
                    EnqueueCommand((int) CommandType.LedGreen, _currentGreenLedState);
                    break;

                case StageKitLedColor.Yellow:
                    _currentYellowLedState = led;
                    EnqueueCommand((int) CommandType.LedYellow, _currentYellowLedState);
                    break;

                case StageKitLedColor.Red:
                    _currentRedLedState = led;
                    EnqueueCommand((int) CommandType.LedRed, _currentRedLedState);
                    break;

                case StageKitLedColor.All:
                    _currentBlueLedState = led;
                    _currentGreenLedState = led;
                    _currentYellowLedState = led;
                    _currentRedLedState = led;
                    EnqueueCommand((int) CommandType.LedBlue, _currentBlueLedState);
                    EnqueueCommand((int) CommandType.LedGreen, _currentGreenLedState);
                    EnqueueCommand((int) CommandType.LedYellow, _currentYellowLedState);
                    EnqueueCommand((int) CommandType.LedRed, _currentRedLedState);
                    break;

                default:
                    YargLogger.LogWarning(" Unknown color: " + color);
                    return;
            }
        }
    }
}

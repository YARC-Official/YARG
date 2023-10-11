using System;
using System.Collections.Generic;
using UnityEngine;
using PlasticBand.Haptics;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;


/*
 Software Layout:
    This controller is in the persistent scene. It is responsible for keeping track of the current state of the stage kits, and sending commands to the stage kit.
    Each scene has its own loader (StagekitMenu, StagekitScore, StagekitGameplay) , which is responsible for loading the lighting cues for that scene. The loader is responsible for setting the CurrentLightingCue variable in this controller.
    Cues are made up of set of primitives that either follow the beat, triggered by an event, or timed.
    Some cues have multiple patterns. These patterns are not random, the same song in the same venue will have the same patterns for the cues each time. I do not know how that decided.
    Each venue is flagged as either Large or Small. This is used to determine which pattern to use for some cues. This is randomized currently, since there is no way to get venue size from the game, at the moment.

 Hardware layout:
    LED numbers on the pod:
    rockband logo
     7 0 1
      \|/
    6 -+-  2
      /|\
     5 4 3
    xbox button
    as well as the fog machine and strobe light. The fog machine has 2 settings, on and off. The strobe light has 5 settings, off, slow, medium, fast, fastest. Only slow and fast are used in offical songs.
    For details on how to send commands to the kit, see the IStageKitHaptics interface and Nate's fantastic work at https://github.com/TheNathannator/PlasticBand/blob/main/Docs/Other/Stage%20Kit/Xbox%20360.md

 Bugs and notes:
    During official light shows (For example, using the stage kit with RB2, on a xbox 360) the lights will sometimes behave in unexpected, song specific, one-off ways.
    It is hard to say if this is a bug or complex intended behavior. Since there are known bugs with the stage kit as is, I am going to assume these are also bugs since the programming required to make these bespoke song effects is not trivial and doesn't seem to make sense.
    So stage shows will be slightly different than the official ones, but hopefully more consistent and predictable with what was intended (and even better!).
    Sometimes pause doesn't work for fast strobe?? I legit don't know why this happens. It seems to be a bug with the stage kit itself.

  Not implemented because these things don't exist YARG:
    "About to fail song" light cues are not implemented since we don't support failing songs right now.
    Intro "walk on" lighting before the song starts (where it says "<user name> as <character name>") is not implemented because that doesn't exist.

 Implemented by YARG but not in the original game:
    Menu lighting


NR = No visible response from Stage Kit
VR = Response changes based on venue size
RE = Response is the same regardless of venue size




This file is for the individual effects for the stage kit:
Keyframed calls:

	NR	verse						Doesn't seem to do anything.
	NR	chorus						Doesn't seem to do anything.

	RE	loop_cool                   2 blue LEDs 180 degrees apart rotating counter clockwise, 1 green led starting at 90 degrees rotating clockwise. To the beat.
	RE	loop_warm                   2 red LEDs 180 degrees apart rotating clockwise, 1 yellow led starting at 90 counter rotating counterclockwise. To the beat.
	RE	manual_cool					2 blue LEDs 180 degrees apart rotating counter clockwise, 1 green led starting at 90 degrees rotating clockwise. To the beat. Does not turn off strobe on initial call, turns it off on [next]
	RE	manual_warm					2 red LEDs 180 degrees apart rotating clockwise, 1 yellow led starting at 90 counter rotating counterclockwise. To the beat. Does not turn off strobe on initial call, turns it off on [next]

	VR	dischord					1 yellow led clock circles on major and minor beat.
									Red ring on drum red fret
	                                Blue follows [next], pattern is 6|2 ,off, 6|2|0|4. Turns off on initial call then on with next. (not 100% sure on this one)

	                                Small venue:
									1 Green led @ 0, counter-clockwise circles to beat.

									Large Venue:
									On Major beat toggles between: 1 Green led@0 counter clock circles to beat | all green leds on.

	VR	stomp						Initial call turns on leds. Responds to the [next], toggling lights on or off.

									Small Venue:
									Red, Green, Yellow

									Large Venue:
									All colors


	VR	Empty (i.e. [lighting ()])	Small Venue:
		Default lighting.			All red on, all blue on, changing on [next]. Yellow ring on, half beat flash on drum red fret
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
	                        For Dischord, It turns on both blue and green rings, with the blue toggling on and off depending on the vocal note end after each major beat.
	                        Does nothing with Stomp
	                        Turns off all lights for everything else.

	VR	searchlights     	On beat.
	                        Small venue pattern:
						    	1 yellow and 1 red rotate together. Counter clock

							Large venue patterns:
								1 yellow@2 clockwise and 1 blue@0 counter clock.

								There are other patterns for both cues that change which leds start. I don't know how they are chosen yet.

	RE	sweep               On beat.
	                        small venue:
		                        Yellow@ 6 and 2, counter-clock
		                        Blue@0, clock

		                    Large venue:
		                        Red @6 and 2, counter-clock

	RE	strobe_slow			Strobe light that blinks every 16th note/120 ticks.
	RE	strobe_fast			Strobe light that blinks every 32nd note/60 ticks.
	                        The strobe_off call is exceedingly rare, the strobe is typically turned off by other cues starting.

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
		                    2 yellow at 180 to each other clock 2 second starting on 6 and 2, 1 blue@0 counter clock 1 second

		                    large venue
		                    2 yellow at 180 to each other clock 2 second, 2 red at 180 counter clock 1 second

	RE Menu lighting        1 blue@0 rotates counter clock every two seconds. Made by me. Not in the original game.
 */
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
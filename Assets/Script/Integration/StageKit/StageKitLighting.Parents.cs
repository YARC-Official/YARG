using System;
using System.Collections.Generic;
using System.Threading;
using YARG.Core.Chart;

namespace YARG
{
    //parent of primitives
    //grandparent of cues
    public abstract class StageKitLighting
    {
		protected const byte NONE  = 0b00000000;
		protected const byte ZERO  = 0b00000001;
		protected const byte ONE   = 0b00000010;
		protected const byte TWO   = 0b00000100;
		protected const byte THREE = 0b00001000;
		protected const byte FOUR  = 0b00010000;
		protected const byte FIVE  = 0b00100000;
		protected const byte SIX   = 0b01000000;
		protected const byte SEVEN = 0b10000000;
		protected const byte ALL   = 0b11111111;

        [Flags]
        public enum ListenTypes
        {
            Next = 1,
            MajorBeat = 2,
            MinorBeat = 4,
            RedFretDrums = 8,
        }

        protected CancellationTokenSource CancellationTokenSource;

        protected virtual void HandleLightingEvent(LightingType eventName)
        {

        }

        protected virtual void HandleBeatlineEvent(BeatlineType eventName)
        {

        }

        protected virtual void HandleDrumEvent(int eventName)
        {

        }

        protected virtual void HandleVocalEvent(double eventName)
        {

        }

        protected virtual void OnBeat()
        {

		}


        protected void Start()
        {
            CancellationTokenSource = new CancellationTokenSource();
            StageKitGameplay.Instance.HandleBeatline += HandleBeatlineEvent;
            StageKitGameplay.Instance.HandleDrums += HandleDrumEvent;
            StageKitGameplay.Instance.HandleLighting += HandleLightingEvent;
            StageKitGameplay.Instance.HandleVocals += HandleVocalEvent;
        }

        public void Dispose()
        {
            CancellationTokenSource?.Cancel();
            StageKitGameplay.Instance.GameManger.BeatEventManager.Unsubscribe(OnBeat);
            StageKitGameplay.Instance.HandleBeatline -= HandleBeatlineEvent;
            StageKitGameplay.Instance.HandleDrums -= HandleDrumEvent;
            StageKitGameplay.Instance.HandleLighting -= HandleLightingEvent;
            StageKitGameplay.Instance.HandleVocals -= HandleVocalEvent;
        }

	}
	public abstract class StageKitLightingCues : StageKitLighting //This is the parent class of all lighting cues. (not primitives)
    {
        public LightingType CurrentCueType;

	    protected const int BLUE = 0;
	    protected const int GREEN = 1;
	    protected const int YELLOW = 2;
	    protected const int RED = 3;

        protected List<StageKitLighting> CuePrimitives = new();

        public void Dispose(bool turnOffLeds = false)
        {
		    base.Dispose();
            CancellationTokenSource?.Cancel();

            CuePrimitives.ForEach(cue => cue?.Dispose());

            if (!turnOffLeds) return;
            StageKitLightingController.Instance.SetLed(RED, NONE);
            StageKitLightingController.Instance.SetLed(GREEN, NONE);
            StageKitLightingController.Instance.SetLed(BLUE, NONE);
            StageKitLightingController.Instance.SetLed(YELLOW, NONE);
        }
    }
}
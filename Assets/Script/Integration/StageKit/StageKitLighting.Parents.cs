using System;
using System.Collections.Generic;
using System.Threading;
using YARG.Core.Chart;

namespace YARG.Integration.StageKit
{
    //parent of primitives
    //grandparent of cues
    public abstract class StageKitLighting
    {
        public CancellationTokenSource CancellationTokenSource;

        protected const byte NONE = 0b00000000;
        protected const byte ZERO = 0b00000001;
        protected const byte ONE = 0b00000010;
        protected const byte TWO = 0b00000100;
        protected const byte THREE = 0b00001000;
        protected const byte FOUR = 0b00010000;
        protected const byte FIVE = 0b00100000;
        protected const byte SIX = 0b01000000;
        protected const byte SEVEN = 0b10000000;
        protected const byte ALL = 0b11111111;

        [Flags]
        public enum ListenTypes
        {
            Next = 1,
            MajorBeat = 2,
            MinorBeat = 4,
            RedFretDrums = 8,
        }

        public virtual void HandleLightingEvent(LightingType eventName)
        {
        }

        public virtual void HandleBeatlineEvent(BeatlineType eventName)
        {
        }

        public virtual void HandleDrumEvent(int eventName)
        {
        }

        public virtual void HandleVocalEvent(double eventName)
        {
        }

        public virtual void OnBeat()
        {
        }
    }

    public abstract class
        StageKitLightingCue : StageKitLighting //This is the parent class of all lighting cues. (not primitives)
    {
        public bool LargeVenue;
        public StageKitLightingCue PreviousLightingCue;

        protected const int BLUE = 0;
        protected const int GREEN = 1;
        protected const int YELLOW = 2;
        protected const int RED = 3;

        public List<StageKitLighting> CuePrimitives = new();
    }
}
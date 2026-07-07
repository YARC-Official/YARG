using System;
using YARG.Input;

namespace YARG.Playback
{
    internal interface IInputClock
    {
        double FrameTime { get; }
        double InstantTime { get; }
        double ClampFrameTimeToInstant();
    }

    internal sealed class UnityInputClock : IInputClock
    {
        private double _minimumFrameTime = double.NegativeInfinity;

        public double FrameTime
        {
            get
            {
                double frameTime = InputManager.InputUpdateTime;
                if (frameTime >= _minimumFrameTime)
                {
                    _minimumFrameTime = double.NegativeInfinity;
                    return frameTime;
                }

                return _minimumFrameTime;
            }
        }

        public double InstantTime => InputManager.EstimatedCurrentInputTime;

        public double ClampFrameTimeToInstant()
        {
            double now = InstantTime;
            _minimumFrameTime = Math.Max(_minimumFrameTime, now);
            return now;
        }
    }
}

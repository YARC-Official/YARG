using System;
using YARG.Input;

namespace YARG.Playback
{
    internal interface IInputClock
    {
        /// <summary>
        /// Input-system time for the current frame, clamped forward after <see cref="ClampFrameTimeToInstant"/>
        /// to avoid reporting a time older than the instant anchor.
        /// </summary>
        double FrameTime { get; }

        /// <summary>
        /// Estimated input-system time at this exact moment, not limited to the current frame update.
        /// </summary>
        double InstantTime { get; }

        /// <summary>
        /// Returns <see cref="InstantTime"/> and temporarily prevents <see cref="FrameTime"/> from reporting
        /// an older value until Unity's frame input time catches up.
        /// </summary>
        double ClampFrameTimeToInstant();
    }

    /// <summary>
    /// Adapter for Unity's input-system clock used by the song timeline.
    /// </summary>
    /// <remarks>
    /// <see cref="InputManager.InputUpdateTime"/> reports the time of the current input update, while
    /// <see cref="InputManager.EstimatedCurrentInputTime"/> estimates the current clock time immediately.
    /// When the timeline anchors to the instant time, such as during resume, the next frame input time can
    /// still be older than that anchor. This class temporarily clamps frame time to the instant-anchor time
    /// so timeline time cannot move backwards while Unity's input update time catches up.
    /// </remarks>
    internal sealed class UnityInputClock : IInputClock
    {
        // NegativeInfinity means no clamp is active. Any real frame time is allowed.
        private double _minimumAllowedFrameTime = double.NegativeInfinity;

        public double FrameTime
        {
            get
            {
                double frameTime = InputManager.InputUpdateTime;
                if (frameTime >= _minimumAllowedFrameTime)
                {
                    _minimumAllowedFrameTime = double.NegativeInfinity;
                    return frameTime;
                }

                return _minimumAllowedFrameTime;
            }
        }

        public double InstantTime => InputManager.EstimatedCurrentInputTime;

        public double ClampFrameTimeToInstant()
        {
            double now = InstantTime;
            _minimumAllowedFrameTime = Math.Max(_minimumAllowedFrameTime, now);
            return now;
        }
    }
}

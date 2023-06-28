using System;
using UnityEngine;

namespace YARG.PlayMode
{
    public class PerformanceTextScaler
    {
        public float AnimTimeRemaining { get; set; }

        private float _animTimeLength;

        // Set whether fontSize represents the "peak" or the "rest" size
        // Note that rest size == peak size * 0.9f for animation purposes
        private bool _representsPeakSize;

        public PerformanceTextScaler(float animationLength, bool representsPeakSize = false)
        {
            _animTimeLength = animationLength;
            _representsPeakSize = representsPeakSize;

            AnimTimeRemaining = 0.0f;
        }

        public void ResetAnimationTime()
        {
            AnimTimeRemaining = _animTimeLength;
        }

        /// <returns>
        /// The font size of the text given the current animation timestamp
        /// </returns>
        /// <summary>
        /// Given an animTimeLenth "a":
        /// - At t = 0s, start from 0% size and start the SHARP climb up to 100% size
        /// - At t = 1/6s, be at 100% size and start the fall down to 90% size
        /// - At t = 1/3s, rest at 90% size
        /// - At t = (a - 1/3)s, start from 90% size and star the climb back up to 100% size
        /// - At t = (a - 1/6)s. be 100% size and start the SHARP fall back down to 0% size
        /// - At t = (a)s, rest at 0% size
        /// a = 1s for vocal performance text (e.g., AWESOME, STRONG, AWFUL)
        /// a = 3s for guitar / drums / keys performance text (e.g., BASS GROOVE, HOT START, STRONG FINISH)
        /// Anim should be symmetrical with respect to the halfway point to prevent snapping, even if under 2/3s
        /// </summary>
        public float PerformanceTextScale()
        {
            // Define the relative size before font sizing is applied
            float relativeSize = 0.0f;
            float halfwayPoint = _animTimeLength / 2.0f;

            // Determine the text size relative to its countdown timestamp
            if ((AnimTimeRemaining > _animTimeLength) || (AnimTimeRemaining < 0.0f))
            {
                relativeSize = 0.0f;
            }
            else if (AnimTimeRemaining >= halfwayPoint)
            {
                if (AnimTimeRemaining > (_animTimeLength - 0.16666f))
                {
                    relativeSize = (-1290.0f * Mathf.Pow(
                        AnimTimeRemaining - _animTimeLength + 0.16666f, 4.0f)) + 0.9984f;
                }
                else
                {
                    float denominator = 1.0f + Mathf.Pow((float) Math.E,
                        -50.0f * (AnimTimeRemaining - _animTimeLength + 0.25f));
                    relativeSize = (0.1f / denominator) + 0.9f;
                }
            }
            else if (AnimTimeRemaining < halfwayPoint)
            {
                if (AnimTimeRemaining < 0.16666f)
                {
                    relativeSize = (-1290.0f * Mathf.Pow(
                        AnimTimeRemaining - 0.16666f, 4.0f)) + 0.9984f;
                }
                else
                {
                    float denominator = 1.0f + Mathf.Pow((float) Math.E,
                        -50.0f * (AnimTimeRemaining - 0.25f));
                    relativeSize = (-0.1f / denominator) + 1.0f;
                }
            }

            // Size appropriately
            return relativeSize * (_representsPeakSize ? 1.0f : 1.11111f);
        }
    }
}
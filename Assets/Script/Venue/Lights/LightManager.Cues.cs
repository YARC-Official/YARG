﻿using UnityEngine;

namespace YARG.Venue
{
    public partial class LightManager
    {
		private Color target;
		private float targetint;

		private LightState Default(LightState current, VenueLightLocation location, Gradient gradient, float startint)
        {
			if (AnimationFrame < 1)
			{
				current.Color = null;
			}
			else if (AnimationFrame % 2 == 0)
			{
				current.Color = location switch
				{
					VenueLightLocation.Right or
					VenueLightLocation.Left or
					VenueLightLocation.Crowd 	=> gradient.Evaluate(Mathf.Repeat(AnimationFrame+1,2)/2f),
					_							=> null
				};
			}
			else
			{
				current.Color = location switch
				{
					VenueLightLocation.Right or
					VenueLightLocation.Left or
					VenueLightLocation.Crowd 	=> null,
					_							=> gradient.Evaluate(Mathf.Repeat(AnimationFrame+1,2)/2f)
				};
			}
			targetint = 1f;
			current.Intensity = Mathf.Lerp(startint, targetint, _timer);

            current.Delta += Time.deltaTime * _gradientLightingSpeed;
            if (current.Delta > 1f)
            {
                current.Delta = 0f;
            }

            return current;
        }

		private LightState AutoGradient(LightState current, VenueLightLocation location, Gradient gradient, Color startcol, float startint, bool auto)
        {
			if (auto)
			{
				target = gradient.Evaluate(current.Delta);
			}
			else
			{
				target = location switch
				{
					VenueLightLocation.Right or
					VenueLightLocation.Left or
					VenueLightLocation.Crowd 	=> gradient.Evaluate(Mathf.Repeat(AnimationFrame+1,2)/2f),
					_							=> gradient.Evaluate(Mathf.Repeat(AnimationFrame,2)/2f)
				};
			}
			targetint = 1f;
			current.Color = Color.Lerp(startcol, target, _timer);
			current.Intensity = Mathf.Lerp(startint, targetint, _timer);

            current.Delta += Time.deltaTime * _gradientLightingSpeed;
            if (current.Delta > 1f)
            {
                current.Delta = 0f;
            }

            return current;
        }

        private LightState AutoGradientSplit(LightState current, VenueLightLocation location,
            Gradient innerGradient, Gradient outerGradient, Color startcol, float startint, bool auto)
        {
            var gradient = location switch
            {
                VenueLightLocation.Right or
                VenueLightLocation.Left or
                VenueLightLocation.Crowd => outerGradient,
                _                        => innerGradient,
            };

            return AutoGradient(current, location, gradient, startcol, startint, auto);
        }

        private LightState BlackOut(LightState current, float startint)
        {
	        current.Intensity = Mathf.Lerp(startint, 0f, _timer);
            return current;
        }

		private LightState BlackOutSpot(LightState current, VenueLightLocation location, Color startcol, float startint)
        {
			if (location == VenueLightLocation.Front)
			{
				target = _silhouetteColor;
				targetint = 1f;
			}
			else if (location == VenueLightLocation.Center)
			{
				target = Color.white;
				targetint = 0.1f;
			}
			else
			{
				targetint = 0f;
			}
			current.Color = Color.Lerp(startcol, target, _timer);
			current.Intensity = Mathf.Lerp(startint, targetint, _timer);
			return current;
        }

        private LightState Flare(LightState current)
        {
	        current.Color = Color.white;
	        current.Intensity = 1f;
            return current;
        }

        private LightState Strobe(LightState current, Color startcol, float startint)
        {
            var strobe = AnimationFrame % 2 == 0 ? 1f : 0f;
            targetint = Mathf.Lerp(current.Intensity, strobe, Time.deltaTime / (0.05f / _BPMAdjust));
            var blendstrobe = Mathf.Lerp(startint, targetint, _timer);
			current.Color = Color.Lerp(startcol, Color.white, _timer);
			current.Intensity = Mathf.Lerp(startint, blendstrobe, _timer);
            return current;
        }

        private LightState Stomp(LightState current, Gradient gradient, Color startcol, float startint)
        {
			target = ((gradient.Evaluate(current.Delta) + Color.white * 2 ) * 0.3f);
            var stomp = AnimationFrame % 2 == 0 ? 1f : 0f;
            targetint = Mathf.Lerp(current.Intensity, stomp, Time.deltaTime / (0.1f / _BPMAdjust));
            var blendstomp = Mathf.Lerp(startint, targetint, _timer);
            current.Color = Color.Lerp(startcol, target, _timer);
            current.Intensity = Mathf.Lerp(startint, blendstomp, _timer);
            return current;
        }

        private LightState Silhouette(LightState current, VenueLightLocation location, Color startcol, float startint)
        {
            if (location == VenueLightLocation.Back)
            {
                targetint = 1f;
                target = _silhouetteColor;
            }
            else
            {
                targetint = 0f;
            }
            current.Color = Color.Lerp(startcol, target, _timer);
            current.Intensity = Mathf.Lerp(startint, targetint, _timer);
            return current;
        }

        private LightState SilhouetteSpot(LightState current, VenueLightLocation location, Color startcol, float startint)
        {
            if (location == VenueLightLocation.Crowd || location == VenueLightLocation.Front || location == VenueLightLocation.Center)
            {
                targetint = 0f;
            }
            else
            {
                targetint = 1f;
                target = location switch
                {
                    VenueLightLocation.Back => Color.white,
                    _                         => _silhouetteColor
                };
            }
            current.Color = Color.Lerp(startcol, target, _timer);
            current.Intensity = Mathf.Lerp(startint, targetint, _timer);
            return current;
        }

        private LightState Searchlights(LightState current, VenueLightLocation location,
            Gradient gradient, Color startcol, float startint)
        {
            targetint = 1f;
            target = location switch
            {
                VenueLightLocation.Right or
                VenueLightLocation.Left  => Color.white,
                _                        => gradient.Evaluate(current.Delta),
            };

			current.Color = Color.Lerp(startcol, target, _timer);
			current.Intensity = Mathf.Lerp(startint, targetint, _timer);

			current.Delta += Time.deltaTime * _gradientLightingSpeed;
            if (current.Delta > 1f)
            {
                current.Delta = 0f;
            }

            return current;
        }
    }
}
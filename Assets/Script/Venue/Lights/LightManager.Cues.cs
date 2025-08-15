using UnityEngine;

namespace YARG.Venue
{
    public partial class LightManager
    {
		private Color target;
		private float targetint;
		
		private LightState Default(LightState current, VenueLightLocation location, Gradient gradient)
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
			current.Intensity = Mathf.Lerp(current.Intensity, targetint, Time.deltaTime * 30f);

            current.Delta += Time.deltaTime * _gradientLightingSpeed;
            if (current.Delta > 1f)
            {
                current.Delta = 0f;
            }

            return current;
        }
		
		private LightState AutoGradient(LightState current, VenueLightLocation location, Gradient gradient)
        {
			if (AnimationFrame < 1)
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
			current.Color = Color.Lerp(current.Color ?? Color.white, target, Time.deltaTime * 25f);
			current.Intensity = Mathf.Lerp(current.Intensity, targetint, Time.deltaTime * 30f);

            current.Delta += Time.deltaTime * _gradientLightingSpeed;
            if (current.Delta > 1f)
            {
                current.Delta = 0f;
            }

            return current;
        }

        private LightState AutoGradientSplit(LightState current, VenueLightLocation location,
            Gradient innerGradient, Gradient outerGradient)
        {
            var gradient = location switch
            {
                VenueLightLocation.Right or
                VenueLightLocation.Left or
                VenueLightLocation.Crowd => outerGradient,
                _                        => innerGradient,
            };

            return AutoGradient(current, location, gradient);
        }

        private LightState BlackOut(LightState current, float speed)
        {
            current.Intensity = Mathf.Lerp(current.Intensity, 0f, Time.deltaTime * speed);
            return current;
        }
		
		private LightState BlackOutSpot(LightState current, float speed, VenueLightLocation location)
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
			current.Color = Color.Lerp(current.Color ?? Color.white, target, Time.deltaTime * speed);
			current.Intensity = Mathf.Lerp(current.Intensity, targetint, Time.deltaTime * speed);
			return current;
        }

        private LightState Flare(LightState current, float speed)
        {
            current.Intensity = Mathf.Lerp(current.Intensity, 1.5f, Time.deltaTime * speed);
            current.Color = Color.Lerp(current.Color ?? Color.white, Color.white, Time.deltaTime * speed);
            return current;
        }

        private LightState Strobe(LightState current)
        {
			target = Color.white;
            targetint = AnimationFrame % 2 == 0 ? 1f : 0f;
			current.Color = Color.Lerp(current.Color ?? Color.white, target, Time.deltaTime * 50f);
			current.Intensity = Mathf.Lerp(current.Intensity, targetint, Time.deltaTime * 50f);
            return current;
        }
		
        private LightState Stomp(LightState current, Gradient gradient)
        {
			target = ((gradient.Evaluate(current.Delta) + Color.white) * 0.5f);
            targetint = AnimationFrame % 2 == 0 ? 1f : 0f;
			current.Color = Color.Lerp(current.Color ?? Color.white, target, Time.deltaTime * 25f);
			current.Intensity = Mathf.Lerp(current.Intensity, targetint, Time.deltaTime * 30f);
            return current;
        }
		
        private LightState Silhouette(LightState current, VenueLightLocation location)
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
			current.Color = Color.Lerp(current.Color ?? Color.white, target, Time.deltaTime * 25f);
			current.Intensity = Mathf.Lerp(current.Intensity, targetint, Time.deltaTime * 30f);
            return current;
        }

        private LightState SilhouetteSpot(LightState current, VenueLightLocation location)
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
			current.Color = Color.Lerp(current.Color ?? Color.white, target, Time.deltaTime * 25f);
			current.Intensity = Mathf.Lerp(current.Intensity, targetint, Time.deltaTime * 30f);
            return current;
        }

        private LightState Searchlights(LightState current, VenueLightLocation location,
            Gradient gradient)
        {
            targetint = 1f;
            target = location switch
            {
                VenueLightLocation.Right or
                VenueLightLocation.Left  => Color.white,
                _                        => gradient.Evaluate(current.Delta),
            };
			
			current.Color = Color.Lerp(current.Color ?? Color.white, target, Time.deltaTime * 25f);
			current.Intensity = Mathf.Lerp(current.Intensity, targetint, Time.deltaTime * 30f);
			
			current.Delta += Time.deltaTime * _gradientLightingSpeed;
            if (current.Delta > 1f)
            {
                current.Delta = 0f;
            }

            return current;
        }
    }
}

using UnityEngine;

namespace YARG.Venue
{
    public partial class LightManager
    {
        private LightState AutoGradient(LightState current, VenueLightLocation location, Gradient gradient)
        {
			if (AnimationFrame < 1)
			{
				current.Color = gradient.Evaluate(current.Delta);
			}
			else
			{
				current.Color = location switch
				{
					VenueLightLocation.Right or
					VenueLightLocation.Left or
					VenueLightLocation.Crowd 	=> gradient.Evaluate(Mathf.Repeat(AnimationFrame+1,2)/2f),
					_							=> gradient.Evaluate(Mathf.Repeat(AnimationFrame,2)/2f)
				};
			}
			current.Intensity = 1f;

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

        private LightState Flare(LightState current, float speed)
        {
            current.Intensity = Mathf.Lerp(current.Intensity, 1f, Time.deltaTime * speed);
            current.Color = Color.Lerp(current.Color ?? Color.white, Color.white, Time.deltaTime * speed);
            return current;
        }

        private LightState Strobe(LightState current)
        {
            current.Intensity = AnimationFrame % 2 == 0 ? 1f : 0f;
            return current;
        }

        private LightState Silhouette(LightState current, VenueLightLocation location)
        {
            if (location == VenueLightLocation.Crowd)
            {
                current.Intensity = 0f;
            }
            else
            {
                current.Intensity = 1f;
                current.Color = location switch
                {
                    VenueLightLocation.Center => Color.white,
                    _                         => _silhouetteColor
                };
            }

            return current;
        }

        private LightState Searchlights(LightState current, VenueLightLocation location,
            Gradient gradient)
        {
            current.Intensity = 1f;
            current.Color = location switch
            {
                VenueLightLocation.Right or
                VenueLightLocation.Left  => Color.white,
                _                        => gradient.Evaluate(current.Delta),
            };
			
			current.Delta += Time.deltaTime * _gradientLightingSpeed;
            if (current.Delta > 1f)
            {
                current.Delta = 0f;
            }

            return current;
        }
    }
}

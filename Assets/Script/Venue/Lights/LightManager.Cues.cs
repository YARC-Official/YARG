using UnityEngine;

namespace YARG.Venue
{
    public partial class LightManager
    {
        private LightState AutoGradient(LightState current, Gradient gradient)
        {
            current.Color = gradient.Evaluate(current.Delta);
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

            return AutoGradient(current, gradient);
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
    }
}

using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Gameplay;
using Random = UnityEngine.Random;

namespace YARG.Venue
{
    public partial class LightManager : GameplayBehaviour
    {
        public struct LightState
        {
            /// <summary>
            /// The intensity of the light between <c>0</c> and <c>1</c>. <c>1</c> is the default value.
            /// </summary>
            public float Intensity;

            /// <summary>
            /// The color of the light. <see cref="Intensity"/> should be taken into consideration.
            /// <c>null</c> indicates default.
            /// </summary>
            public Color? Color;

            public float Delta;
        }

        public LightingType Animation { get; private set; }
        public int AnimationFrame { get; private set; }

        private LightState[] _lightStates;
        public LightState GenericLightState => _lightStates[(int) VenueLightLocation.Generic];

        [SerializeField]
        private float _gradientLightingSpeed = 0.125f;
        [SerializeField]
        private float _gradientRandomness = 0.5f;

        [Space]
        [SerializeField]
        private Color[] _warmColors;
        [SerializeField]
        private Color[] _coolColors;

        private List<LightingEvent> _lightingEvents;

        private Gradient _warmGradient;
        private Gradient _coolGradient;

        private int _lightingEventIndex;
        private int _beatIndex;

        protected override void OnChartLoaded(SongChart chart)
        {
            _lightStates = new LightState[EnumExtensions<VenueLightLocation>.Count];

            _lightingEvents = chart.VenueTrack.Lighting;

            // If the color arrays are empty, add basic ones for safety

            if (_warmColors.Length <= 0)
            {
                _warmColors = new[]
                {
                    Color.red,
                    Color.yellow
                };
            }

            if (_coolColors.Length <= 0)
            {
                _coolColors = new[]
                {
                    Color.blue,
                    Color.green
                };
            }

            // Setup gradients
            _warmGradient = CreateGradient(_warmColors);
            _coolGradient = CreateGradient(_coolColors);

            // 1/8th of a beat is a 32nd note
            GameManager.BeatEventHandler.Subscribe(UpdateLightAnimation, new(1f / 8f));
        }

        protected override void GameplayDestroy()
        {
            GameManager.BeatEventHandler.Unsubscribe(UpdateLightAnimation);
        }

        private void Update()
        {
            // Look for new lighting events
            while (_lightingEventIndex < _lightingEvents.Count &&
                _lightingEvents[_lightingEventIndex].Time <= GameManager.VisualTime)
            {
                var current = _lightingEvents[_lightingEventIndex];

                switch (current.Type)
                {
                    case LightingType.Keyframe_Next:
                        AnimationFrame++;
                        break;
                    case LightingType.Keyframe_Previous:
                        AnimationFrame--;
                        break;
                    case LightingType.Keyframe_First:
                        AnimationFrame = 0;
                        break;
                    case LightingType.Warm_Automatic:
                    case LightingType.Warm_Manual:
                    case LightingType.Cool_Automatic:
                    case LightingType.Cool_Manual:
                    case LightingType.Verse:
                    case LightingType.Chorus:
                        // Add a slight randomness to colored cues
                        for (int i = 0; i < _lightStates.Length; i++)
                        {
                            _lightStates[i].Delta = Random.Range(0f, _gradientRandomness);
                        }

                        goto default;
                    default:
                        Animation = current.Type;
                        AnimationFrame = 0;
                        break;
                }

                _lightingEventIndex++;
            }

            UpdateLightStates();
        }

        private void UpdateLightAnimation()
        {
            _beatIndex++;

            switch (Animation)
            {
                case LightingType.Strobe_Fast:
                    AnimationFrame++;
                    break;
                case LightingType.Strobe_Slow:
                    if (_beatIndex % 2 == 1)
                    {
                        AnimationFrame++;
                    }

                    break;
            }
        }

        private void UpdateLightStates()
        {
            for (int i = 0; i < _lightStates.Length; i++)
            {
                var location = (VenueLightLocation) i;

                switch (Animation)
                {
                    case LightingType.Blackout_Fast:
                        _lightStates[i] = BlackOut(_lightStates[i], 15f);
                        break;
                    case LightingType.Blackout_Slow:
                        _lightStates[i] = BlackOut(_lightStates[i], 10f);
                        break;
                    case LightingType.Flare_Fast:
                        _lightStates[i] = Flare(_lightStates[i], 15f);
                        break;
                    case LightingType.Flare_Slow:
                        _lightStates[i] = Flare(_lightStates[i], 10f);
                        break;
                    case LightingType.Stomp:
                    case LightingType.Strobe_Fast:
                    case LightingType.Strobe_Slow:
                        _lightStates[i] = Strobe(_lightStates[i]);
                        break;
                    case LightingType.Warm_Automatic:
                    case LightingType.Warm_Manual:
                        _lightStates[i] = GradientAutomatic(_lightStates[i], _warmGradient);
                        break;
                    case LightingType.Cool_Automatic:
                    case LightingType.Cool_Manual:
                        _lightStates[i] = GradientAutomatic(_lightStates[i], _coolGradient);
                        break;
                    case LightingType.Verse:
                        _lightStates[i] = SplitGradient(_lightStates[i], location, _coolGradient, _warmGradient);
                        break;
                    case LightingType.Chorus:
                        _lightStates[i] = SplitGradient(_lightStates[i], location, _warmGradient, _coolGradient);
                        break;
                    default:
                        _lightStates[i].Intensity = 1f;
                        _lightStates[i].Color = null;
                        _lightStates[i].Delta = 0f;
                        break;
                }
            }
        }

        public LightState GetLightStateFor(VenueLightLocation location)
        {
            return _lightStates[(int) location];
        }

        private static Gradient CreateGradient(Color[] colors)
        {
            var gradient = new Gradient();

            var keys = new GradientColorKey[colors.Length + 1];

            // Make the gradient loop nice without snapping
            keys[0] = new GradientColorKey(colors[^1], 0f);

            // Add the rest of the colors
            for (int i = 1; i < keys.Length; i++)
            {
                keys[i] = new GradientColorKey(colors[i - 1], 1f / colors.Length * i);
            }

            // No alpha for gradient
            gradient.SetKeys(keys, new[]
            {
                new GradientAlphaKey(1f, 0f)
            });

            return gradient;
        }
    }
}
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Gameplay;
using YARG.Playback;
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
		public LightState LeftLightState => _lightStates[(int) VenueLightLocation.Left];
		public LightState RightLightState => _lightStates[(int) VenueLightLocation.Right];
		public LightState FrontLightState => _lightStates[(int) VenueLightLocation.Front];
		public LightState BackLightState => _lightStates[(int) VenueLightLocation.Center];
		public LightState CenterLightState => _lightStates[(int) VenueLightLocation.Back];
		public LightState CrowdLightState => _lightStates[(int) VenueLightLocation.Crowd];

        [SerializeField]
        private float _gradientLightingSpeed = 0.125f;
		
		private float _initialGradientSpeed;
		
        [SerializeField]
        private float _gradientRandomness = 0.5f;

        [Space]
        [SerializeField]
        private Color[] _warmColors;
        [SerializeField]
        private Color[] _coolColors;
        [SerializeField]
        private Color[] _dissonantColors;
        [SerializeField]
        private Color[] _harmoniousColors;
        [SerializeField]
        private Color _silhouetteColor;

        private List<LightingEvent> _lightingEvents;

        private Gradient _warmGradient;
        private Gradient _coolGradient;
        private Gradient _dissonantGradient;
        private Gradient _harmoniousGradient;

        private int _lightingEventIndex;
        private int _beatIndex;

        protected override void OnChartLoaded(SongChart chart)
        {
            _lightStates = new LightState[EnumExtensions<VenueLightLocation>.Count];

            _lightingEvents = chart.VenueTrack.Lighting;

            // If the color arrays are empty, add basic ones for safety

            if (_warmColors is not { Length: > 0 })
            {
                _warmColors = new[]
                {
                    Color.red,
                    Color.yellow
                };
            }

            if (_coolColors is not { Length: > 0 })
            {
                _coolColors = new[]
                {
                    Color.blue,
                    Color.green
                };
            }

            if (_dissonantColors is not { Length: > 0 })
            {
                _dissonantColors = new[]
                {
                    Color.red,
                    Color.green,
                    Color.blue,
                };
            }

            if (_harmoniousColors is not { Length: > 0 })
            {
                _harmoniousColors = new[]
                {
                    Color.yellow,
                    Color.red,
                    Color.blue,
                };
            }			
		
			// Store gradient speed for temporary Frenzy/BRE speedup
			_initialGradientSpeed = _gradientLightingSpeed;

            // Setup gradients
            _warmGradient = CreateGradient(_warmColors);
            _coolGradient = CreateGradient(_coolColors);
            _dissonantGradient = CreateGradient(_dissonantColors);
            _harmoniousGradient = CreateGradient(_harmoniousColors);

            // 1/8th of a beat is a 32nd note
            GameManager.BeatEventHandler.Subscribe(UpdateLightAnimation, 1f / 8f, mode: TempoMapEventMode.Quarter);
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
					case LightingType.Searchlights:
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
                    case LightingType.Verse:
                        _lightStates[i] = AutoGradientSplit(_lightStates[i], location, _coolGradient, _warmGradient);
						_gradientLightingSpeed = _initialGradientSpeed;
                        break;
                    case LightingType.Chorus:
                        _lightStates[i] = AutoGradientSplit(_lightStates[i], location, _warmGradient, _coolGradient);
						_gradientLightingSpeed = _initialGradientSpeed;
                        break;
                    case LightingType.Blackout_Fast:
                        _lightStates[i] = BlackOut(_lightStates[i], 15f);
                        break;
                    case LightingType.Blackout_Slow:
                        _lightStates[i] = BlackOut(_lightStates[i], 10f);
                        break;
                    case LightingType.Dischord:
						_lightStates[i] = AutoGradient(_lightStates[i], location, _dissonantGradient);
						_gradientLightingSpeed = _initialGradientSpeed;
						break;
                    case LightingType.BigRockEnding:
                        _lightStates[i] = AutoGradientSplit(_lightStates[i], location, _dissonantGradient, _harmoniousGradient);
						_gradientLightingSpeed = _initialGradientSpeed*8f;
                        break;
                    case LightingType.Frenzy:
                        _lightStates[i] = AutoGradientSplit(_lightStates[i], location, _dissonantGradient, _harmoniousGradient);
						_gradientLightingSpeed = _initialGradientSpeed*4f;
                        break;
                    case LightingType.Cool_Automatic:
                    case LightingType.Cool_Manual:
					case LightingType.Sweep:
                        _lightStates[i] = AutoGradient(_lightStates[i], location, _coolGradient);
						_gradientLightingSpeed = _initialGradientSpeed;
                        break;
                    case LightingType.Flare_Fast:
                        _lightStates[i] = Flare(_lightStates[i], 15f);
                        break;
                    case LightingType.Flare_Slow:
                        _lightStates[i] = Flare(_lightStates[i], 10f);
                        break;
                    case LightingType.Harmony:
                        _lightStates[i] = AutoGradient(_lightStates[i], location, _harmoniousGradient);
						_gradientLightingSpeed = _initialGradientSpeed;
                        break;
                    case LightingType.Silhouettes:
                    case LightingType.Silhouettes_Spotlight:
                        _lightStates[i] = Silhouette(_lightStates[i], location);
                        break;
					case LightingType.Searchlights:
						_lightStates[i] = Searchlights(_lightStates[i], location, _warmGradient);
						break;
                    case LightingType.Strobe_Fast:
                    case LightingType.Strobe_Slow:
                    case LightingType.Stomp:
                        _lightStates[i] = Strobe(_lightStates[i]);
                        break;
                    case LightingType.Warm_Automatic:
                    case LightingType.Warm_Manual:
                        _lightStates[i] = AutoGradient(_lightStates[i], location, _warmGradient);
						_gradientLightingSpeed = _initialGradientSpeed;
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

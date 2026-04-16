using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.Logging;
using YARG.Gameplay;
using YARG.Helpers;
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

        private readonly Dictionary<Performer, VenueSpotLightLocation> _spotlightLocations = new()
        {
            { Performer.Bass, VenueSpotLightLocation.Bass },
            { Performer.Drums, VenueSpotLightLocation.Drums },
            { Performer.Guitar, VenueSpotLightLocation.Guitar },
            { Performer.Vocals, VenueSpotLightLocation.Vocals },
        };

        public LightingType Animation { get; private set; }
        public int AnimationFrame { get; private set; }


        // Double because spots stay on for the duration of the event and then turn off without an off event, so we store time
        private double[]     _spotlightStates;
        private LightState[] _lightStates;
        public  LightState   GenericLightState => _lightStates[(int) VenueLightLocation.Generic];
		public  LightState   LeftLightState    => _lightStates[(int) VenueLightLocation.Left];
		public  LightState   RightLightState   => _lightStates[(int) VenueLightLocation.Right];
		public  LightState   FrontLightState   => _lightStates[(int) VenueLightLocation.Front];
		public  LightState   BackLightState    => _lightStates[(int) VenueLightLocation.Back];
		public  LightState   CenterLightState  => _lightStates[(int) VenueLightLocation.Center];
		public  LightState   CrowdLightState   => _lightStates[(int) VenueLightLocation.Crowd];

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

        private List<LightingEvent>  _lightingEvents;
        private List<PerformerEvent> _performerEvents;
        private List<TempoChange> _tempoList;

        private Color[] _currentColors;
        private float[] _currentIntensity;
        private LightingType _prevLight;

        private Gradient _warmGradient;
        private Gradient _coolGradient;
        private Gradient _dissonantGradient;
        private Gradient _harmoniousGradient;

        private int _lightingEventIndex;
        private int _performerEventIndex;
        private int _beatIndex;
        private int _tempoIndex;
        private double _currentTempo;
        private float _BPMAdjust;
        private float _blendTime;
        private float _timer;
        private bool _blending;
        private bool _auto;

        protected override void OnChartLoaded(SongChart chart)
        {
            _lightStates = new LightState[EnumExtensions<VenueLightLocation>.Count];
            _spotlightStates = new double[EnumExtensions<VenueSpotLightLocation>.Count];

            _lightingEvents = chart.VenueTrack.Lighting;
            _performerEvents = chart.VenueTrack.Performer;
            _tempoList = chart.SyncTrack.Tempos;

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

            _currentColors = new[]
            {
                Color.white,
                Color.white,
                Color.white,
                Color.white,
                Color.white,
                Color.white,
                Color.white,
            };

            _currentIntensity = new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f };

			// Store gradient speed for temporary Frenzy/BRE speedup
			_initialGradientSpeed = _gradientLightingSpeed;

            // Setup gradients
            _warmGradient = CreateGradient(_warmColors);
            _coolGradient = CreateGradient(_coolColors);
            _dissonantGradient = CreateGradient(_dissonantColors);
            _harmoniousGradient = CreateGradient(_harmoniousColors);

            // 1/8th of a beat is a 32nd note
            GameManager.BeatEventHandler.Visual.Subscribe(UpdateLightAnimation, BeatEventType.QuarterNote, division: 1f / 8f);
        }

        protected override void GameplayDestroy()
        {
            GameManager.BeatEventHandler.Visual.Unsubscribe(UpdateLightAnimation);
        }

        private void Update()
        {
            while (_tempoList.Count > 0 && _tempoIndex < _tempoList.Count &&
                   _tempoList[_tempoIndex].Time <= GameManager.VisualTime)
            {
                _currentTempo = _tempoList[_tempoIndex].BeatsPerMinute;
                _tempoIndex++;
                _BPMAdjust = (float)_currentTempo / 60;
            }

            // Look for new lighting events
            while (_lightingEventIndex < _lightingEvents.Count &&
                _lightingEvents[_lightingEventIndex].Time <= GameManager.VisualTime)
            {
                var current = _lightingEvents[_lightingEventIndex];
                _blendTime = 0.1f / _BPMAdjust;

                for (int i = 0; i < _lightStates.Length; i++)
                {
                    _currentColors[i] = _lightStates[i].Color ?? Color.white;
                    _currentIntensity[i] = _lightStates[i].Intensity;
                }

                if (_lightingEventIndex + 1 < _lightingEvents.Count)
                {
                    var nextlight = _lightingEvents[_lightingEventIndex + 1];
                    var nextindex = _lightingEventIndex + 1;

                    while (nextindex < _lightingEvents.Count && (nextlight.Type == LightingType.KeyframeFirst ||
                                                                 nextlight.Type == LightingType.KeyframeNext ||
                                                                 nextlight.Type == LightingType.KeyframePrevious))
                    {
                        nextlight = _lightingEvents[nextindex];
                        nextindex++;
                    }

                    if (current.Type == _prevLight && current.Type != LightingType.KeyframeFirst &&
                        current.Type != LightingType.KeyframeNext && current.Type != LightingType.KeyframePrevious &&
                        current.Type != nextlight.Type)
                    {
                        _auto = false;
                        _blendTime = (float)(nextlight.Time - current.Time);
                        _blending = true;
                        switch (nextlight.Type)
                        {
                            case LightingType.WarmAutomatic:
                            case LightingType.CoolAutomatic:
                            case LightingType.Sweep:
                            case LightingType.Searchlights:
                            case LightingType.Frenzy:
                            case LightingType.BigRockEnding:
                                // Add a slight randomness to automatic cues
                                for (int i = 0; i < _lightStates.Length; i++)
                                {
                                    _lightStates[i].Delta = Random.Range(0f, _gradientRandomness);
                                }
                                _auto = true;
                                goto default;
                            default:
                                Animation = nextlight.Type;
                                AnimationFrame = 0;
                                StartCoroutine(BlendTimerBool(_blendTime));
                                break;
                        }
                    }
                }

                if (_blending == false)
                {
                    _auto = false;
                    switch (current.Type)
                    {
                        case LightingType.KeyframeNext:
                            _blendTime = 0.25f / _BPMAdjust;
                            StartCoroutine(BlendTimer(_blendTime));
                            AnimationFrame++;
                            break;
                        case LightingType.KeyframePrevious:
                            _blendTime = 0.25f / _BPMAdjust;
                            StartCoroutine(BlendTimer(_blendTime));
                            AnimationFrame--;
                            break;
                        case LightingType.KeyframeFirst:
                            _blendTime = 0.25f / _BPMAdjust;
                            StartCoroutine(BlendTimer(_blendTime));
                            AnimationFrame = 0;
                            break;
                        case LightingType.WarmAutomatic:
                        case LightingType.CoolAutomatic:
                        case LightingType.Sweep:
                        case LightingType.Searchlights:
                        case LightingType.Harmony:
                        case LightingType.Frenzy:
                        case LightingType.BigRockEnding:
                            // Add a slight randomness to automatic cues
                            for (int i = 0; i < _lightStates.Length; i++)
                            {
                                _lightStates[i].Delta = Random.Range(0f, _gradientRandomness);
                            }
                            _auto = true;
                            goto default;
                        default:
                            Animation = current.Type;
                            AnimationFrame = 0;
                            StartCoroutine(BlendTimer(_blendTime));
                            break;
                    }
                }
                _prevLight = current.Type;
                _lightingEventIndex++;
            }

            // Decrement the spotlight times
            for (int i = 0; i < _spotlightStates.Length; i++)
            {
                if (_spotlightStates[i] <= 0)
                {
                    continue;
                }

                _spotlightStates[i] -= Time.deltaTime;
            }

            // Look for new performer events
            // TODO: Fix the event parsing so that Time and TimeEnd aren't backwards (with the attendant negative length)
            while (_performerEventIndex < _performerEvents.Count &&
                _performerEvents[_performerEventIndex].Time <= GameManager.VisualTime)
            {
                var current = _performerEvents[_performerEventIndex];
                if (current.Type != PerformerEventType.Spotlight)
                {
                    _performerEventIndex++;
                    continue;
                }
                if (!_spotlightLocations.TryGetValue(current.Performers, out var location))
                {
                    _performerEventIndex++;
                    continue;
                }

                _spotlightStates[(int) location] = current.TimeLength;

                _performerEventIndex++;
            }

            UpdateLightStates();
        }

        private void UpdateLightAnimation()
        {
            _beatIndex++;

            switch (Animation)
            {
                case LightingType.StrobeFast:
                    AnimationFrame++;
                    break;
                case LightingType.StrobeSlow:
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
					case LightingType.Default:
                    case LightingType.Intro:
						_lightStates[i] = Default(_lightStates[i], location, _harmoniousGradient, _currentIntensity[i]);
						break;
                    case LightingType.Verse:
                        _lightStates[i] = AutoGradientSplit(_lightStates[i], location, _harmoniousGradient, _dissonantGradient,
                            _currentColors[i], _currentIntensity[i], _auto);
						_gradientLightingSpeed = _initialGradientSpeed;
                        break;
                    case LightingType.Chorus:
                        _lightStates[i] = AutoGradientSplit(_lightStates[i], location, _warmGradient, _coolGradient,
                            _currentColors[i], _currentIntensity[i], _auto);
						_gradientLightingSpeed = _initialGradientSpeed;
                        break;
                    case LightingType.BlackoutFast:
                    case LightingType.BlackoutSlow:
                        _lightStates[i] = BlackOut(_lightStates[i], _currentIntensity[i]);
                        break;
                    case LightingType.BlackoutSpotlight:
                        _lightStates[i] = BlackOutSpot(_lightStates[i], location,
                            _currentColors[i], _currentIntensity[i]);
                        break;
                    case LightingType.Dischord:
						_lightStates[i] = AutoGradient(_lightStates[i], location, _dissonantGradient,
                            _currentColors[i], _currentIntensity[i], _auto);
						_gradientLightingSpeed = _initialGradientSpeed;
						break;
                    case LightingType.BigRockEnding:
                        _lightStates[i] = AutoGradientSplit(_lightStates[i], location, _dissonantGradient, _harmoniousGradient,
                            _currentColors[i], _currentIntensity[i], _auto);
						_gradientLightingSpeed = _initialGradientSpeed * (4f * _BPMAdjust);
                        break;
                    case LightingType.Frenzy:
                        _lightStates[i] = AutoGradientSplit(_lightStates[i], location, _warmGradient, _coolGradient,
                            _currentColors[i], _currentIntensity[i], _auto);
						_gradientLightingSpeed = _initialGradientSpeed * (2f * _BPMAdjust);
                        break;
                    case LightingType.CoolAutomatic:
                    case LightingType.CoolManual:
                        _lightStates[i] = AutoGradient(_lightStates[i], location, _coolGradient,
                            _currentColors[i], _currentIntensity[i], _auto);
                        _gradientLightingSpeed = _initialGradientSpeed;
                        break;
					case LightingType.Sweep:
                        _lightStates[i] = AutoGradientSplit(_lightStates[i], location, _coolGradient, _harmoniousGradient,
                            _currentColors[i], _currentIntensity[i], _auto);
						_gradientLightingSpeed = _initialGradientSpeed;
                        break;
                    case LightingType.FlareFast:
                    case LightingType.FlareSlow:
                        _lightStates[i] = Flare(_lightStates[i]);
                        break;
                    case LightingType.Harmony:
                        _lightStates[i] = AutoGradient(_lightStates[i], location, _harmoniousGradient,
                            _currentColors[i], _currentIntensity[i], _auto);
						_gradientLightingSpeed = _initialGradientSpeed;
                        break;
                    case LightingType.Silhouettes:
                        _lightStates[i] = Silhouette(_lightStates[i], location,
                            _currentColors[i], _currentIntensity[i]);
                        break;
                    case LightingType.SilhouettesSpotlight:
                        _lightStates[i] = SilhouetteSpot(_lightStates[i], location,
                            _currentColors[i], _currentIntensity[i]);
                        break;
					case LightingType.Searchlights:
						_lightStates[i] = Searchlights(_lightStates[i], location, _warmGradient,
                            _currentColors[i], _currentIntensity[i]);
						_gradientLightingSpeed = _initialGradientSpeed;
						break;
                    case LightingType.StrobeFast:
                    case LightingType.StrobeSlow:
                        _lightStates[i] = Strobe(_lightStates[i], _currentColors[i], _currentIntensity[i]);
                        break;
                    case LightingType.Stomp:
						_lightStates[i] = Stomp(_lightStates[i], _warmGradient,
                            _currentColors[i], _currentIntensity[i]);
						break;
                    case LightingType.WarmAutomatic:
                    case LightingType.WarmManual:
                        _lightStates[i] = AutoGradient(_lightStates[i], location, _warmGradient,
                            _currentColors[i], _currentIntensity[i], _auto);
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

        public bool GetSpotlightStateFor(VenueSpotLightLocation location)
        {
            return _spotlightStates[(int) location] > 0;
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

        private IEnumerator BlendTimerBool(float time)
        {
            _timer = 0f;
            while (_timer < 1f)
            {
                _timer += Time.deltaTime / time;
                yield return null;
            }
            if (_blending == true && _timer >= 1f)
            {
                yield return new WaitForSeconds(0.05f);
                _blending = false;
            }
        }

        private IEnumerator BlendTimer(float time)
        {
            _timer = 0f;
            while (_timer < 1f)
            {
                _timer += Time.deltaTime / time;
                yield return null;
            }
        }
    }
}

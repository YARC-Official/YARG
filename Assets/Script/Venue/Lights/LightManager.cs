using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay;

namespace YARG.Venue
{
    public class LightManager : GameplayBehaviour
    {
        public class LightState
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
        }

        public LightingType Animation { get; private set; }
        public int AnimationFrame { get; private set; }

        public LightState MainLightState { get; private set; }

        private List<LightingEvent> _lightingEvents;

        private int _lightingEventIndex;
        private int _beatIndex;

        protected override void OnChartLoaded(SongChart chart)
        {
            MainLightState = new LightState();

            _lightingEvents = chart.VenueTrack.Lighting;

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
                    default:
                        Debug.Log($"Event set to {current.Type}");
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
                case LightingType.Frenzy:
                case LightingType.Flare_Fast:
                case LightingType.BigRockEnding:
                    AnimationFrame++;
                    break;
                case LightingType.Strobe_Slow:
                case LightingType.Flare_Slow:
                    if (_beatIndex % 2 == 1)
                    {
                        AnimationFrame++;
                    }

                    break;
            }
        }

        private void UpdateLightStates()
        {
            MainLightState.Color = null;

            switch (Animation)
            {
                case LightingType.Blackout_Fast:
                    MainLightState.Intensity = Mathf.Lerp(MainLightState.Intensity, 0f, Time.deltaTime * 15f);
                    break;
                case LightingType.Blackout_Slow:
                    MainLightState.Intensity = Mathf.Lerp(MainLightState.Intensity, 0f, Time.deltaTime * 10f);
                    break;
                case LightingType.Stomp:
                case LightingType.Flare_Slow:
                case LightingType.Dischord:
                case LightingType.Searchlights:
                case LightingType.Sweep:
                case LightingType.Silhouettes:
                case LightingType.Strobe_Fast:
                case LightingType.Frenzy:
                case LightingType.Flare_Fast:
                case LightingType.BigRockEnding:
                case LightingType.Strobe_Slow:
                    MainLightState.Intensity = AnimationFrame % 2 == 0 ? 1f : 0f;
                    break;
                default:
                    MainLightState.Intensity = 1f;
                    break;
            }
        }
    }
}
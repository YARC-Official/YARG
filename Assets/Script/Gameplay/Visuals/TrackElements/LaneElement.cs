using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public class LaneElement : TrackElement<TrackPlayer>
    {
        // Maximum time in seconds where consecutive lanes at the same note index should be combined
        public const float COMBINE_LANE_THRESHOLD = 0.1f;

        // Conversion rate from end cap bone movement units to 1 TrackElement.GetZPositionAtTime unit
        private const float LANE_LENGTH_RATIO = 0.02f;

        private static readonly int _emissionEnabled = Shader.PropertyToID("_Emission");
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");
        
        private static Dictionary<Instrument,float> _scaleByInstrument = new();

        public static void DefineLaneScale(Instrument instrument, int subdivisions)
        {
            if (_scaleByInstrument.ContainsKey(instrument))
            {
                return;
            }
            
            float laneScaleX = TrackPlayer.TRACK_WIDTH / subdivisions;
            _scaleByInstrument.Add(instrument, laneScaleX);
        }

        [SerializeField]
        private Transform _scaleTransform;

        [SerializeField]
        private Transform _endCapPlacement;

        [Space]
        [SerializeField]
        private SkinnedMeshRenderer _meshRenderer;

        public override double ElementTime => _startTime;
        [HideInInspector]
        public double EndTime;

        protected override float RemovePointOffset => _zLength;

        private int _startIndex;
        private int _endIndex = -1;

        private double _startTime;

        private float _xPosition;
        private float _xOffset;

        private float _zLength;

        private float _scale;

        private Color _color;

        public void SetAppearance(Instrument instrument, int index, int subdivisions, Color color)
        {
            _xPosition = GetElementX(index, subdivisions);

            SetAppearance(instrument, index, _xPosition, color);
        }

        public void SetAppearance(Instrument instrument, int index, float xPosition, Color color)
        {
            _startIndex = index;
            _xPosition = xPosition;
            _scale = _scaleByInstrument[instrument];
            _color = color;
        }

        public void MultiplyScale(float scaleOffset)
        {
            _scale *= scaleOffset;

            if (Initialized)
            {
                RenderScale();
            }
        }

        public void SetTimeRange(double startTime, double endTime)
        {
            _startTime = startTime;
            EndTime = endTime;
            
            _zLength = GetZPositionAtTime(endTime) - GetZPositionAtTime(startTime);

            if (Initialized)
            {
                RenderLength();
            }
        }

        public void SetIndexRange(int startIndex, int endIndex)
        {
            if (endIndex == startIndex)
            {
                endIndex = -1;
            }

            _startIndex = startIndex;
            _endIndex = endIndex;
        }

        public bool ContainsIndex(int index)
        {
            if (_endIndex == -1)
            {
                return index == _startIndex;
            }

            return index >= _startIndex && index <= _endIndex;
        }

        public void SetXPosition(float position)
        {
            if (position == _xPosition)
            {
                return;
            }

            _xPosition = position;

            if (Initialized)
            {
                transform.localPosition = transform.localPosition.WithX(_xPosition + _xOffset);
            }
        }

        public void OffsetXPosition(float offset)
        {
            if (offset == _xOffset)
            {
                return;
            }

            _xOffset = offset;

            if (Initialized)
            {
                transform.localPosition = transform.localPosition.WithX(_xPosition + _xOffset);
            }
        }

        protected void RenderLength()
        {
            _endCapPlacement.localPosition = _endCapPlacement.localPosition.WithY(_zLength * LANE_LENGTH_RATIO / _scale);
        }

        protected void RenderScale()
        {
            // Set scale
            _scaleTransform.localScale = new Vector3(_scale, 1f, _scale);
            
            // Recalculate length from new scale
            RenderLength();
        }

        protected override void InitializeElement()
        {
            // Set position
            // Prevent mesh overlap with adjacent lanes
            transform.localPosition = transform.localPosition.WithX(_xPosition + _xOffset);

            RenderScale();

            // Initialize materials
            for (int i = 0; i < _meshRenderer.materials.Length; i++)
            {
                var thisMaterial = _meshRenderer.materials[i];

                thisMaterial.SetFade(Player.ZeroFadePosition, Player.FadeSize);

                if (i == 0)
                {
                    // Set color
                    thisMaterial.color = _color;
                    thisMaterial.SetColor(_emissionColor, _color);
                }
            }
        }

        protected override void UpdateElement()
        {
        }

        protected override void HideElement()
        {
        }
    }
}
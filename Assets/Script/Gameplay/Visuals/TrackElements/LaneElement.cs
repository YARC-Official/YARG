using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Gameplay.Player;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public class LaneElement : TrackElement<TrackPlayer>
    {
        // Maximum time in seconds where consecutive lanes at the same note index should be combined
        public const float COMBINE_LANE_THRESHOLD = 0.1f;

        // Sizes of nested objects in the prefab
        private const float LANE_Y = 0.003f;
        private const float SEGMENT_BASE_WIDTH = 1f;
        private const float SEGMENT_BASE_LENGTH = 1f;

        private static readonly int _emissionEnabled = Shader.PropertyToID("_Emission");
        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");
        
        private static Dictionary<Instrument,float> _scaleByInstrument = new();

        public static void DefineLaneScale(Instrument instrument, int subdivisions)
        {
            if (_scaleByInstrument.ContainsKey(instrument))
            {
                return;
            }
            
            float laneScaleX = (TrackPlayer.TRACK_WIDTH / subdivisions) / SEGMENT_BASE_WIDTH;
            _scaleByInstrument.Add(instrument, laneScaleX);
        }

        [SerializeField]
        private Transform _scaleTransform; // Pivot point at lane start position
        [SerializeField]
        private Transform _lineTransform; // Pivot point at end of start cap
        [SerializeField]
        private Transform _endCapTransform; // Pivot point at lane end position

        [Space]
        [SerializeField]
        private MeshEmissionMaterialIndex[] _coloredMaterials;

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
            // Set line length
            float lineScale = _zLength / SEGMENT_BASE_LENGTH / _scale;
            _lineTransform.localScale = _lineTransform.localScale.WithZ(lineScale);

            // Place end cap
            _endCapTransform.localPosition = _endCapTransform.localPosition.WithZ(SEGMENT_BASE_LENGTH * lineScale);
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
            // Prevent overlapping elements in adjacent lanes
            int overlapModifier = _startIndex % 2;

            // Set position
            // Prevent mesh overlap with adjacent lanes
            transform.localPosition = new Vector3(_xPosition + _xOffset, LANE_Y + LANE_Y * overlapModifier);

            RenderScale();

            // Set color
            foreach (var info in _coloredMaterials)
            {
                var coloredMaterial = info.Mesh.materials[info.MaterialIndex];
                coloredMaterial.color = _color;
                coloredMaterial.SetColor(_emissionColor, _color);
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
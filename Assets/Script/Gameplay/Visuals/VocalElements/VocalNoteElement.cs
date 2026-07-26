using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Gameplay.Visuals
{
    public class VocalNoteElement : VocalElement
    {
        private static readonly int Dimensions = Shader.PropertyToID("_Dimensions");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int GlowColor = Shader.PropertyToID("_GlowColor");
        private const float NOTE_POINT_PADDING = 1f / 15f;

        public VocalNote NoteRef { get; set; }

        public override double ElementTime => NoteRef.Time;

        protected override float RemovePointOffset => VocalTrack.GetPosForTime(NoteRef.TotalTimeLength);

        [SerializeField]
        private LineRenderer[] _lineRenderers;
        [SerializeField]
        private float[] _lineWidthMultipliers;

        [SerializeField]
        private LineRenderer _glowLineRenderer;
        [SerializeField]
        private float _glowWidthMultiplier;

        private readonly List<Vector3> _points = new();
        private readonly List<Vector3> _glowPoints = new();

        protected override void InitializeElement()
        {
            var color = Player.VocalTrack.Colors[NoteRef.HarmonyPart];
            MaterialPropertyInstance.Instance.Clear();
            MaterialPropertyInstance.Instance.SetColor(BaseColor, color);

            // Set line color
            foreach (var line in _lineRenderers)
            {
                line.SetPropertyBlock(MaterialPropertyInstance.Instance);
            }

            YargLogger.Assert(_lineRenderers.Length == _lineWidthMultipliers.Length,
                "Line renderer count does not match width multiplier count!");
            UpdateLinePoints();
        }

        public void SetSpGlow(bool isSp)
        {
            var color = Player.VocalTrack.Colors[NoteRef.HarmonyPart];
            if (isSp)
            {
                MaterialPropertyInstance.Instance.SetColor(GlowColor, Color.gold);
            }
            else
            {
                MaterialPropertyInstance.Instance.SetColor(GlowColor, color);
            }
            _glowLineRenderer.SetPropertyBlock(MaterialPropertyInstance.Instance);
        }

        public void UpdateLinePoints()
        {
            // Create points
            _points.Clear();
            _glowPoints.Clear();
            var length = 0f;
            Vector3? lastPoint = null;
            foreach (var note in NoteRef.AllNotes)
            {
                var z = VocalTrack.GetPosForPitch(note.Pitch);
                var p1 = new Vector3(VocalTrack.GetPosForTime(note.Time - NoteRef.Time), 0f, z);
                var p2 = new Vector3(VocalTrack.GetPosForTime(note.TimeEnd - NoteRef.Time), 0f, z);
                _points.Add(p1);
                _points.Add(p2);
                _glowPoints.Add(p1);
                _glowPoints.Add(p2);
                if (lastPoint.HasValue)
                {
                    length += Vector3.Distance(lastPoint.Value, p1);
                }
                length += Vector3.Distance(p1, p2);
                lastPoint = p2;
            }

            float width = VocalTrack.CurrentNoteWidth;

            // Add padding on the note (start and end)
            if (_points.Count >= 2)
            {
                _points[0] = _points[0].AddX(NOTE_POINT_PADDING);
                _points[^1] = _points[^1].AddX(-NOTE_POINT_PADDING);

                _glowPoints[0] = _glowPoints[0].AddX(-width);
                _glowPoints[^1] = _glowPoints[^1].AddX(width);

                // Add the padding to our tracked total length
                length += 2 * width;
            }

            // Set line info
            for (int lineIndex = 0; lineIndex < _lineRenderers.Length; lineIndex++)
            {
                var line = _lineRenderers[lineIndex];

                // Would have liked to just use widthMultiplier here, but
                // that doesn't seem to work correctly for some reason
                float lineWidth = width * _lineWidthMultipliers[lineIndex];
                line.startWidth = lineWidth;
                line.endWidth = lineWidth;

                line.positionCount = _points.Count;
                for (int pointIndex = 0; pointIndex < _points.Count; pointIndex++)
                {
                    line.SetPosition(pointIndex, _points[pointIndex]);
                }
            }

            var glowLineWidth = width * _glowWidthMultiplier;
            _glowLineRenderer.startWidth = glowLineWidth;
            _glowLineRenderer.endWidth = glowLineWidth;
            _glowLineRenderer.positionCount = _glowPoints.Count;
            for (int pointIndex = 0; pointIndex < _glowPoints.Count; pointIndex++)
            {
                _glowLineRenderer.SetPosition(pointIndex, _glowPoints[pointIndex]);
            }
            MaterialPropertyInstance.Instance.SetVector(Dimensions, new Vector2(length, glowLineWidth));
            _glowLineRenderer.SetPropertyBlock(MaterialPropertyInstance.Instance);
        }

        protected override void UpdateElement()
        {
        }

        protected override void HideElement()
        {
        }
    }
}
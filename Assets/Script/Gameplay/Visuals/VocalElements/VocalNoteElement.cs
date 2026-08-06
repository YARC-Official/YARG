using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Gameplay.Visuals
{
    public class VocalNoteElement : VocalElement
    {
        private static readonly int Dimensions    = Shader.PropertyToID("_Dimensions");
        private static readonly int BaseColor     = Shader.PropertyToID("_BaseColor");
        private static readonly int GlowColor     = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowIntensity = Shader.PropertyToID("_GlowIntensity");

        private const float SP_GLOW_INTENSITY     = 1f;
        private const float NORMAL_GLOW_INTENSITY = 0.4f;

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
            YargLogger.Assert(_glowLineRenderer, "Glow line renderer is null!");
            UpdateLinePoints();
        }

        public void SetSpGlow(bool isSp)
        {
            var color = Player.VocalTrack.Colors[NoteRef.HarmonyPart];
            if (isSp)
            {
                MaterialPropertyInstance.Instance.SetColor(GlowColor, Color.lightGoldenRod);
                MaterialPropertyInstance.Instance.SetFloat(GlowIntensity, SP_GLOW_INTENSITY);
            }
            else
            {
                MaterialPropertyInstance.Instance.SetColor(GlowColor, color);
                MaterialPropertyInstance.Instance.SetFloat(GlowIntensity, NORMAL_GLOW_INTENSITY);
            }
            _glowLineRenderer.SetPropertyBlock(MaterialPropertyInstance.Instance);
        }

        public void UpdateLinePoints()
        {
            // Create points
            _points.Clear();
            var glowLength = 0f;
            Vector3? lastPoint = null;
            foreach (var note in NoteRef.AllNotes)
            {
                var z = VocalTrack.GetPosForPitch(note.Pitch);
                var p1 = new Vector3(VocalTrack.GetPosForTime(note.Time - NoteRef.Time), 0f, z);
                var p2 = new Vector3(VocalTrack.GetPosForTime(note.TimeEnd - NoteRef.Time), 0f, z);
                _points.Add(p1);
                _points.Add(p2);
                if (lastPoint.HasValue)
                {
                    glowLength += Vector3.Distance(lastPoint.Value, p1);
                }

                glowLength += (p2.x - p1.x);
                lastPoint = p2;
            }

            float width = VocalTrack.CurrentNoteWidth;

            // Add padding on the note (start and end)
            if (_points.Count >= 2)
            {
                _points[0] = _points[0].AddX(NOTE_POINT_PADDING);
                _points[^1] = _points[^1].AddX(-NOTE_POINT_PADDING);

                glowLength += 2 * width;
            }

            // Set line info
            for (int lineIndex = 0; lineIndex < _lineRenderers.Length; lineIndex++)
            {
                var line = _lineRenderers[lineIndex];

                // Would have liked to just use widthMultiplier here, but
                // that doesn't seem to work correctly for some reason
                line.widthMultiplier = width * _lineWidthMultipliers[lineIndex];


                line.positionCount = _points.Count;
                line.SetPositions(_points.ToArray());
            }

            var glowLineWidth = width * _glowWidthMultiplier;
            _glowLineRenderer.widthMultiplier = glowLineWidth;
            _glowLineRenderer.positionCount = _points.Count;
            var endPadding = (width * 0.75f) + NOTE_POINT_PADDING;
            _glowLineRenderer.SetPosition(0, _points[0].AddX(-endPadding));
            for (int pointIndex = 1; pointIndex < _points.Count - 1; pointIndex++)
            {
                _glowLineRenderer.SetPosition(pointIndex, _points[pointIndex]);
            }
            _glowLineRenderer.SetPosition(_points.Count - 1, _points[^1].AddX(endPadding));

            MaterialPropertyInstance.Instance.SetVector(Dimensions, new Vector2(glowLength, glowLineWidth));
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
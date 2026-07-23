using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Gameplay.Visuals
{
    public class VocalNoteElement : VocalElement
    {
        private static readonly int   Dimensions         = Shader.PropertyToID("_Dimensions");
        private static readonly int   BaseColor          = Shader.PropertyToID("_BaseColor");
        private const           float NOTE_POINT_PADDING = 1f / 15f;

        public VocalNote NoteRef { get; set; }

        public override double ElementTime => NoteRef.Time;

        protected override float RemovePointOffset => VocalTrack.GetPosForTime(NoteRef.TotalTimeLength);

        [SerializeField]
        private LineRenderer[] _lineRenderers;
        [SerializeField]
        private float[] _lineWidthMultipliers;

        [SerializeField]
        private LineRenderer _spLineRenderer;
        [SerializeField]
        private float _spLineWidthMultiplier;

        private readonly List<Vector3> _points   = new();
        private readonly List<Vector3> _spPoints = new();
        private          bool          _isSp;

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

        public void SetSpGlow(bool enableGlow)
        {
            _spLineRenderer.enabled = enableGlow;
            _isSp = enableGlow;
        }

        public void UpdateLinePoints()
        {
            // Create points
            _points.Clear();
            _spPoints.Clear();
            var length = 0f;
            foreach (var note in NoteRef.AllNotes)
            {
                var z = VocalTrack.GetPosForPitch(note.Pitch);
                var p1 = new Vector3(VocalTrack.GetPosForTime(note.Time - NoteRef.Time), 0f, z);
                var p2 = new Vector3(VocalTrack.GetPosForTime(note.TimeEnd - NoteRef.Time), 0f, z);
                _points.Add(p1);
                _points.Add(p2);
                _spPoints.Add(p1);
                _spPoints.Add(p2);
                length += Vector3.Distance(p1, p2);
            }

            // Add padding on the note (start and end)
            if (_points.Count >= 2)
            {
                _points[0] = _points[0].AddX(NOTE_POINT_PADDING);
                _points[^1] = _points[^1].AddX(-NOTE_POINT_PADDING);

                _spPoints[0] = _spPoints[0].AddX(-NOTE_POINT_PADDING);
                _spPoints[^1] = _spPoints[^1].AddX(NOTE_POINT_PADDING);

                // Add the padding to our tracked total length
                length += NOTE_POINT_PADDING * 2f;
            }

            // Set line info
            float width = VocalTrack.CurrentNoteWidth;
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

            if (_isSp)
            {
                YargLogger.LogFormatDebug("Updating SP line renderer for note at time {0}", NoteRef.Time);
                float lineWidth = width * _spLineWidthMultiplier;
                _spLineRenderer.startWidth = lineWidth;
                _spLineRenderer.endWidth = lineWidth;
                _spLineRenderer.positionCount = _spPoints.Count;
                for (int pointIndex = 0; pointIndex < _spPoints.Count; pointIndex++)
                {
                    _spLineRenderer.SetPosition(pointIndex, _spPoints[pointIndex]);
                }

                // Fetch the active block, add our dimensions, and re-apply
                _spLineRenderer.GetPropertyBlock(MaterialPropertyInstance.Instance);
                MaterialPropertyInstance.Instance.SetVector(Dimensions, new Vector2(length, lineWidth));
                _spLineRenderer.SetPropertyBlock(MaterialPropertyInstance.Instance);
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

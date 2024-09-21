using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Gameplay.Visuals
{
    public class VocalNoteElement : VocalElement
    {
        private const float NOTE_POINT_PADDING = 1f / 15f;

        public VocalNote NoteRef { get; set; }

        public override double ElementTime => NoteRef.Time;

        protected override float RemovePointOffset => VocalTrack.GetPosForTime(NoteRef.TotalTimeLength);

        [SerializeField]
        private LineRenderer[] _lineRenderers;
        [SerializeField]
        private float[] _lineWidthMultipliers;

        private readonly List<Vector3> _points = new();

        protected override void InitializeElement()
        {
            var color = VocalTrack.Colors[NoteRef.HarmonyPart];

            // Set line color
            foreach (var line in _lineRenderers)
            {
                line.material.color = color;
            }

            YargLogger.Assert(_lineRenderers.Length == _lineWidthMultipliers.Length);
            UpdateLinePoints();
        }

        public void UpdateLinePoints()
        {
            // Create points
            _points.Clear();
            foreach (var note in NoteRef.AllNotes)
            {
                var z = VocalTrack.GetPosForPitch(note.Pitch);

                _points.Add(new Vector3(VocalTrack.GetPosForTime(note.Time - NoteRef.Time),    0f, z));
                _points.Add(new Vector3(VocalTrack.GetPosForTime(note.TimeEnd - NoteRef.Time), 0f, z));
            }

            // Add padding on the note (start and end)
            if (_points.Count >= 2)
            {
                _points[0] = _points[0].AddX(NOTE_POINT_PADDING);
                _points[^1] = _points[^1].AddX(-NOTE_POINT_PADDING);
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
        }

        protected override void UpdateElement()
        {
        }

        protected override void HideElement()
        {
        }
    }
}
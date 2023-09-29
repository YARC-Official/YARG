using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay.Visuals
{
    public class VocalNoteElement : VocalElement
    {
        // TODO: Temporary until color profiles for vocals
        private static readonly Color[] _colors =
        {
            new(0.008f, 0.710f, 0.937f, 1f),
            new(0.667f, 0.286f, 0.012f, 1f),
            new(0.886f, 0.561f, 0.090f, 1f)
        };

        public VocalNote NoteRef { get; set; }

        protected override double ElementTime => NoteRef.Time;

        [SerializeField]
        private LineRenderer[] _lineRenderers;

        private readonly List<Vector3> _points = new();

        protected override void InitializeElement()
        {
            var color = _colors[NoteRef.HarmonyPart];

            // Set line color
            foreach (var line in _lineRenderers)
            {
                line.material.color = color;
            }

            UpdateLinePoints();
        }

        private void UpdateLinePoints()
        {
            // Create points
            _points.Clear();
            foreach (var note in NoteRef.ChordEnumerator())
            {
                var z = VocalTrack.GetPosForPitch(note.Pitch);

                _points.Add(new Vector3(VocalTrack.GetPosForTime(note.Time - NoteRef.Time),    0f, z));
                _points.Add(new Vector3(VocalTrack.GetPosForTime(note.TimeEnd - NoteRef.Time), 0f, z));
            }

            // Set line info
            foreach (var line in _lineRenderers)
            {
                line.positionCount = _points.Count;
                for (int i = 0; i < _points.Count; i++)
                {
                    line.SetPosition(i, _points[i]);
                }
            }
        }

        protected override void UpdateElement()
        {
            if (VocalTrack.IsRangeChanging)
            {
                UpdateLinePoints();
            }
        }

        protected override void HideElement()
        {
        }
    }
}
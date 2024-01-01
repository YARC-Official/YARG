using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;

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

        private readonly List<Vector3> _points = new();

        protected override void InitializeElement()
        {
            var color = VocalTrack.Colors[NoteRef.HarmonyPart];

            // Set line color
            foreach (var line in _lineRenderers)
            {
                line.material.color = color;
            }

            UpdateLinePoints();
        }

        public void UpdateLinePoints()
        {
            // Create points
            _points.Clear();
            foreach (var note in NoteRef.ChordEnumerator())
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
        }

        protected override void HideElement()
        {
        }
    }
}
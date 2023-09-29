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

        [SerializeField]
        private LineRenderer[] _lineRenderers;

        protected override double ElementTime => NoteRef.Time;

        protected override void InitializeElement()
        {
            var color = _colors[NoteRef.HarmonyPart];

            // Create points
            var points = new List<Vector3>();
            foreach (var note in NoteRef.ChordEnumerator())
            {
                var z = VocalTrack.GetPosForPitch(note.Pitch);

                points.Add(new Vector3(VocalTrack.GetPosForTime(note.Time - NoteRef.Time),    0f, z));
                points.Add(new Vector3(VocalTrack.GetPosForTime(note.TimeEnd - NoteRef.Time), 0f, z));
            }

            // Set line info
            foreach (var line in _lineRenderers)
            {
                // Set colors
                line.material.color = color;

                // Set points
                line.positionCount = points.Count;
                line.SetPositions(points.ToArray());
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
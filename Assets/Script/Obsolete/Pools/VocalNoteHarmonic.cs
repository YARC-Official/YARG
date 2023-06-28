using System.Collections.Generic;
using UnityEngine;
using YARG.PlayMode;

namespace YARG.Pools
{
    public class VocalNoteHarmonic : Poolable
    {
        [SerializeField]
        private LineRenderer lineRenderer;

        private float lengthCache;

        public void SetInfo(List<(float, (float note, int octave))> pitchOverTime, float length)
        {
            float timeMul = MicPlayer.trackSpeed / Play.speed;

            // Get length
            length *= timeMul;
            lengthCache = length;

            // Get line positions
            var points = new List<Vector3>();
            float z = 0f;
            foreach (var (time, (note, octave)) in pitchOverTime)
            {
                z = MicPlayer.NoteAndOctaveToZ(note, octave);

                float x = time * timeMul;
                if (x == 0f)
                {
                    x = 1f / 15f;
                }

                points.Add(new Vector3(x, 0f, z));
            }

            // Finish off line
            points.Add(new Vector3(length - 1f / 15f, 0f, z));

            // Set points
            lineRenderer.positionCount = points.Count;
            lineRenderer.SetPositions(points.ToArray());
        }

        public void SetColor(int harmIndex)
        {
            // Set line color
            var lineColor = lineRenderer.material.color;
            var harmColor = MicPlayer.HarmonicColors[harmIndex];
            harmColor.a = lineColor.a;
            lineRenderer.material.color = harmColor;
        }

        private void Update()
        {
            transform.localPosition -= new Vector3(Time.deltaTime * MicPlayer.trackSpeed, 0f, 0f);

            if (transform.localPosition.x < -12f - lengthCache)
            {
                MoveToPool();
            }
        }
    }
}
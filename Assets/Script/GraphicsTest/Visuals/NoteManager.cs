using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.GraphicsTest.Instancing;

namespace YARG.GraphicsTest
{
    public class NoteManager
    {
        private readonly MeshInstancer _instancer;
        private readonly Camera _camera;

        private readonly List<GuitarNote> _notes;

        private readonly float _noteSpeed;
        private readonly float _strikelinePosition;

        private readonly Vector3 _baseScale = new(7.5f, 7.5f, 7.5f);

        private readonly float[] _lanePositions = new float[6] {
            0,
            -1.25f,
            -0.625f,
            0,
            0.625f,
            1.25f,
        };

        private readonly Vector3[] _laneScales = new Vector3[6] {
            new(5f, 1f, 1f),
            new(1f, 1f, 1f),
            new(1f, 1f, 1f),
            new(1f, 1f, 1f),
            new(1f, 1f, 1f),
            new(1f, 1f, 1f),
        };

        private readonly Color[] _laneColors = new Color[6] {
            Color.magenta,
            Color.green,
            Color.red,
            Color.yellow,
            new(0.0f, 0.5f, 1.0f),
            new(1.0f, 0.45f, 0.0f),
        };

        private readonly ChartEventTimeTracker<GuitarNote> _spawnTracker;
        private readonly ChartEventTimeTracker<GuitarNote> _despawnTracker;
        private readonly double _spawnTimeOffset;
        private readonly double _despawnTimeOffset;

        public NoteManager(Mesh mesh, Material material, Camera camera,
            List<GuitarNote> notes, float noteSpeed, float strikelinePosition,
            double spawnTimeOffset, double despawnTimeOffset)
        {
            _instancer = new(mesh, material);
            _camera = camera;

            _notes = notes;

            _noteSpeed = noteSpeed;
            _strikelinePosition = strikelinePosition;

            _spawnTracker = new(notes);
            _despawnTracker = new(notes);

            _spawnTimeOffset = spawnTimeOffset;
            _despawnTimeOffset = despawnTimeOffset;
        }

        public void Update(double songTime)
        {
            _instancer.BeginDraw();

            _despawnTracker.Update(songTime + _despawnTimeOffset);

            // Existing notes
            for (int i = _despawnTracker.CurrentIndex; !_instancer.AtCapacity && i < _spawnTracker.CurrentIndex; i++)
            {
                RenderNote(songTime, _notes[i]);
            }

            // New notes
            while (!_instancer.AtCapacity && _spawnTracker.UpdateOnce(songTime + _spawnTimeOffset))
            {
                RenderNote(songTime, _spawnTracker.Current);
            }

            _instancer.EndDraw(_camera);
        }

        private void RenderNote(double songTime, GuitarNote note)
        {
            if (_instancer.AtCapacity)
                return;

            RenderSingleNote(songTime, note);

            foreach (var child in note.ChildNotes)
            {
                if (_instancer.AtCapacity)
                    return;

                RenderSingleNote(songTime, child);
            }
        }

        private void RenderSingleNote(double songTime, GuitarNote note)
        {
            float distance = _strikelinePosition + (float) (note.Time - songTime) * _noteSpeed;
            var position = new Vector3(_lanePositions[note.Fret], 0.1f, distance);

            var rotation = Quaternion.Euler(-90, 0, 0);

            var scale = _laneScales[note.Fret];
            scale.Scale(_baseScale);

            _instancer.RenderInstance(position, rotation, scale, _laneColors[note.Fret]);
        }

        public void ResetToTime(double songTime)
        {
            _spawnTracker.ResetToTime(songTime + _spawnTimeOffset);
            _despawnTracker.ResetToTime(songTime + _despawnTimeOffset);
        }
    }
}

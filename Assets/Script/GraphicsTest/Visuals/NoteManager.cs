using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using YARG.Core.Chart;
using YARG.Rendering;

namespace YARG.GraphicsTest
{
    public class NoteManager : IDisposable
    {
        private readonly MeshInstancer _instancer;

        private readonly List<GuitarNote> _notes;

        private readonly float _noteSpeed;
        private readonly float _strikelinePosition;

        private readonly float[] _lanePositions = new float[6] {
            0,
            -1.27f,
            -0.635f,
            0,
            0.635f,
            1.27f,
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

        public NoteManager(MeshInstancer instancer, List<GuitarNote> notes,
            float noteSpeed, float strikelinePosition,
            double spawnTimeOffset, double despawnTimeOffset)
        {
            _instancer = instancer;

            _notes = notes;

            _noteSpeed = noteSpeed;
            _strikelinePosition = strikelinePosition;

            _spawnTracker = new(notes);
            _despawnTracker = new(notes);

            _spawnTimeOffset = spawnTimeOffset;
            _despawnTimeOffset = despawnTimeOffset;
        }

        public void Dispose()
        {
            _instancer.Dispose();
        }

        public void Update(double songTime)
        {
            _instancer.Clear();

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

            _instancer.Draw(new Bounds(Vector3.zero, Vector3.one * 1000),
                shadowMode: ShadowCastingMode.Off, receiveShadows: false,
                lightProbing: LightProbeUsage.Off);
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
            var position = new Vector3(_lanePositions[note.Fret], 0, distance);

            _instancer.AddInstance(position, Quaternion.identity, _laneScales[note.Fret], _laneColors[note.Fret]);
        }

        public void ResetToTime(double songTime)
        {
            _spawnTracker.ResetToTime(songTime + _spawnTimeOffset);
            _despawnTracker.ResetToTime(songTime + _despawnTimeOffset);
        }
    }
}

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using YARG.Chart;
using YARG.Data;

namespace YARG.PlayMode
{
    /// <summary>
    /// Tracks sustain notes' progress, counted in beats.
    /// </summary>
    public class SustainTracker
    {
        private List<Beat> _beats;
        private Dictionary<AbstractInfo, double> noteProgress = new();

        public SustainTracker(List<Beat> beats)
        {
            _beats = beats;
        }

        /// <summary>
        /// Begin tracking this note. Should be called on note strum.
        /// </summary>
        /// <param name="note"></param>
        /// <returns>Initial sustain beats for this note, compensated for strum offset above 0.</returns>
        public double Strum(AbstractInfo note)
        {
            var initialVal = math.clamp(
                Mathf.Min(Play.Instance.SongTime - note.time, note.length) * Play.Instance.CurrentBeatsPerSecond,
                0.0, double.MaxValue
            );
            noteProgress[note] = initialVal;

            return initialVal;
        }

        /// <summary>
        /// Update tracking of note. Call this on frames where the note's sustain is still active.
        /// </summary>
        /// <param name="note"></param>
        /// <returns>Beats achieved for this frame. Will return 0 if the note sustain is done.</returns>
        public double Update(AbstractInfo note)
        {
            // TODO: account for multiple tempo changes between this frame and last frame (for lag spikes)
            double remainingBeats = note.LengthInBeats - noteProgress[note];
            // pt/b * s * b/s = pt
            double beatsThisFrame = math.min(Time.deltaTime * Play.Instance.CurrentBeatsPerSecond, remainingBeats);
            noteProgress[note] += beatsThisFrame;
            return beatsThisFrame;
        }

        /// <summary>
        /// Stop tracking this note. Use when we don't care about the sustain scoring for this note anymore.
        /// </summary>
        /// <param name="note"></param>
        public void Drop(AbstractInfo note)
        {
            noteProgress.Remove(note);
        }
    }
}
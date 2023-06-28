using System.Collections.Generic;
using UnityEngine;

namespace YARG.Data
{
    public class LyricInfo : AbstractInfo
    {
        public string lyric;
        public bool inharmonic;

        public List<(float, (float note, int octave))> pitchOverTime;

        /// <returns>
        /// Returns the pitch at the specified <paramref name="relativeTime"/> within the note.
        /// </returns>
        public float GetLerpedNoteAtTime(float relativeTime)
        {
            // Get the first index (of the two we are in between) 
            int first = 0;
            for (int i = 0; i < pitchOverTime.Count; i++)
            {
                first = i;
                if (pitchOverTime[i].Item1 > relativeTime)
                {
                    break;
                }
            }

            // Get the second index
            int second;
            float secondLength;
            if (first >= pitchOverTime.Count - 1)
            {
                second = first;
                secondLength = length;
            }
            else
            {
                second = first + 1;
                secondLength = pitchOverTime[second].Item1;
            }

            // Lerp between them
            float timeIntoSection = relativeTime - pitchOverTime[first].Item1;
            float sectionLength = secondLength - pitchOverTime[first].Item1;

            return Mathf.Lerp(
                pitchOverTime[first].Item2.note + pitchOverTime[first].Item2.octave * 12f,
                pitchOverTime[second].Item2.note + pitchOverTime[second].Item2.octave * 12f,
                timeIntoSection / sectionLength
            );
        }

        /// <returns>
        /// Returns the relative note and its octave at the specified <paramref name="relativeTime"/> within the note.
        /// </returns>
        public (float note, int octave) GetLerpedAndSplitNoteAtTime(float relativeTime)
        {
            float outNote = GetLerpedNoteAtTime(relativeTime);
            int octave = 0;

            while (outNote > 12f)
            {
                octave++;
                outNote -= 12f;
            }

            return (outNote, octave);
        }
    }
}
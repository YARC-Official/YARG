using System.Collections.Generic;
using UnityEngine;

namespace YARG.Pools
{
    public class VocalNotePool : Pool
    {
        public Transform AddNoteInharmonic(float length, float x, bool isHarmony, int harmIndex)
        {
            var yOffset = harmIndex * 0.01f;
            var poolable = (VocalNoteInharmonic) Add("note_inharmonic", new Vector3(x, 0.065f - yOffset, 0.1825f));
            poolable.SetLength(length);
            poolable.SetHarmony(isHarmony);
            poolable.SetColor(harmIndex);

            return poolable.transform;
        }

        public Transform AddNoteHarmonic(List<(float, (float, int))> pitchOverTime, float length, float x,
            int harmIndex)
        {
            var yOffset = harmIndex * 0.01f;
            var poolable = (VocalNoteHarmonic) Add("note_harmonic", new Vector3(x, 0.07f - yOffset, 0f));
            poolable.SetInfo(pitchOverTime, length);
            poolable.SetColor(harmIndex);

            return poolable.transform;
        }

        public Transform AddEndPhraseLine(float x)
        {
            var poolable = Add("endPhraseLine", new Vector3(x, 0.1f, 0f));
            return poolable.transform;
        }
    }
}
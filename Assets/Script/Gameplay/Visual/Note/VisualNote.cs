using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay
{
    public abstract class VisualNote<TNote> : MonoBehaviour where TNote : Note<TNote>
    {
        public TNote Note { get; private set; }
    }
}
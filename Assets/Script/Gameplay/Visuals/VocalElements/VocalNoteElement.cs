using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay.Visuals
{
    public class VocalNoteElement : VocalElement
    {
        public VocalNote NoteRef { get; set; }

        protected override double ElementTime => NoteRef.Time;

        protected override void InitializeElement()
        {
        }

        protected override void UpdateElement()
        {
        }

        protected override void HideElement()
        {
        }
    }
}
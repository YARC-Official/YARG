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
            throw new System.NotImplementedException();
        }

        protected override void HideElement()
        {
            throw new System.NotImplementedException();
        }

        protected override void UpdateElement()
        {
            throw new System.NotImplementedException();
        }
    }
}
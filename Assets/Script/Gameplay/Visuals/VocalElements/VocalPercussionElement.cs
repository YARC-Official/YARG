using YARG.Core.Chart;

namespace YARG.Gameplay.Visuals
{
    public class VocalPercussionElement : VocalElement
    {
        public VocalNote NoteRef { get; set; }

        public override double ElementTime => NoteRef.Time;

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
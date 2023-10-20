using YARG.Core.Chart;

namespace YARG.Gameplay.Visuals
{
    public class PhraseLineElement : VocalElement
    {
        public VocalsPhrase PhraseRef;

        public override double ElementTime => PhraseRef.TimeEnd;

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
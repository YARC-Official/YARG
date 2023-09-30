using YARG.Core.Engine;
using YARG.Core.Input;

namespace YARG.Gameplay.Player
{
    public class VocalPlayer : BasePlayer
    {
        public override BaseStats Stats { get; }

        public override float[] StarMultiplierThresholds { get; }
        public override int[] StarScoreThresholds { get; protected set; }

        protected override void ResetVisuals()
        {
            throw new System.NotImplementedException();
        }

        public override void ResetPracticeSection()
        {
            throw new System.NotImplementedException();
        }

        protected override void UpdateInputs(double time)
        {
            throw new System.NotImplementedException();
        }

        protected override void UpdateVisuals(double time)
        {
            throw new System.NotImplementedException();
        }

        public override void SetPracticeSection(uint start, uint end)
        {
            throw new System.NotImplementedException();
        }

        public override void SetReplayTime(double time)
        {
            throw new System.NotImplementedException();
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            return false;
        }

        protected override void OnInputProcessed(ref GameInput input)
        {
            throw new System.NotImplementedException();
        }
    }
}
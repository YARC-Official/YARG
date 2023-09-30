using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Input;
using YARG.Player;

namespace YARG.Gameplay.Player
{
    public class VocalsPlayer : BasePlayer
    {
        public override BaseStats Stats { get; }

        public override float[] StarMultiplierThresholds { get; } =
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.18f
        };

        public override int[] StarScoreThresholds { get; protected set; }

        public new void Initialize(int index, YargPlayer player, SongChart chart)
        {
            if (IsInitialized) return;

            base.Initialize(index, player, chart);

            StarScoreThresholds = new int[StarMultiplierThresholds.Length];
            for (int i = 0; i < StarMultiplierThresholds.Length; i++)
            {
                // TODO:
                // StarScoreThresholds[i] = Mathf.FloorToInt(Engine.BaseScore * StarMultiplierThresholds[i]);
            }
        }

        protected override void ResetVisuals()
        {
        }

        public override void ResetPracticeSection()
        {
        }

        protected override void UpdateInputs(double time)
        {
        }

        protected override void UpdateVisuals(double time)
        {
        }

        public override void SetPracticeSection(uint start, uint end)
        {
        }

        public override void SetReplayTime(double time)
        {
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            return false;
        }

        protected override void OnInputProcessed(ref GameInput input)
        {
        }
    }
}
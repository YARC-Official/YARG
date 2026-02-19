using YARG.Core.Engine.Vocals;

namespace YARG.Menu.ScoreScreen
{
    public class VocalsScoreCard : ScoreCard<VocalsStats>
    {
        public override void SetCardContents()
        {
            base.SetCardContents();
            SetAdvancedStatsVisible(false);
        }
    }
}
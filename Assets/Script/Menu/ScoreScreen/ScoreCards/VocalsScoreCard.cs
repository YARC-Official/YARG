using UnityEngine;
using YARG.Core.Engine.Vocals;

namespace YARG.Menu.ScoreScreen
{
    public class VocalsScoreCard : ScoreCard<VocalsStats>
    {
        public override void SetCardContents()
        {
            base.SetCardContents();

            var totalTicks = Stats.TicksHit + Stats.TicksMissed;
            AccuracyPercent.text = $"{Mathf.FloorToInt((float) Stats.TicksHit / totalTicks * 100f)}%";
        }
    }
}
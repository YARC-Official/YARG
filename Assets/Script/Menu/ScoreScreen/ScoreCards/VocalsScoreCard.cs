using UnityEngine;
using YARG.Core.Engine.Vocals;

namespace YARG.Menu.ScoreScreen
{
    public class VocalsScoreCard : ScoreCard<VocalsStats>
    {
        public override void SetCardContents()
        {
            base.SetCardContents();

            var totalTicks = Stats.VocalTicksHit + Stats.VocalTicksMissed;
            AccuracyPercent.text = $"{Mathf.FloorToInt((float) Stats.VocalTicksHit / totalTicks * 100f)}%";
        }
    }
}
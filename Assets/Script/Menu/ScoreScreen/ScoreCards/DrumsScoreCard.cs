using TMPro;
using UnityEngine;
using YARG.Core.Engine.Drums;

namespace YARG.Menu.ScoreScreen
{
    public class DrumsScoreCard : ScoreCard<DrumsStats>
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _overhits;

        public override void SetCardContents()
        {
            base.SetCardContents();

            _overhits.text = Stats.Overhits.ToString();
        }
    }
}
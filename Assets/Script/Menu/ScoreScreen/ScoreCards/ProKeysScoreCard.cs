using TMPro;
using UnityEngine;
using YARG.Core.Engine.ProKeys;

namespace YARG.Menu.ScoreScreen
{
    public class ProKeysScoreCard : ScoreCard<ProKeysStats>
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _overhits;

        public override void SetCardContents()
        {
            base.SetCardContents();

            _overhits.text = WrapWithColor(Stats.Overhits);
        }
    }
}
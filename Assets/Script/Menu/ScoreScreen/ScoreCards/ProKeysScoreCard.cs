using TMPro;
using UnityEngine;
using YARG.Core.Engine.Keys;

namespace YARG.Menu.ScoreScreen
{
    public class ProKeysScoreCard : ScoreCard<KeysStats>
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
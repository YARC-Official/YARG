using TMPro;
using UnityEngine;
using YARG.Core.Engine.Guitar;

namespace YARG.Menu.StatsScreen
{
    public class GuitarStatsCard : StatsCard<GuitarStats>
    {
        [SerializeField]
        private TextMeshProUGUI _overstrums;

        [SerializeField]
        private TextMeshProUGUI _hoposStrummed;

        [SerializeField]
        private TextMeshProUGUI _ghostInputs;

        public override void SetCardContents()
        {
            base.SetCardContents();

            _overstrums.text = Stats.Overstrums.ToString();
            _hoposStrummed.text = Stats.HoposStrummed.ToString();
            _ghostInputs.text = Stats.GhostInputs.ToString();
        }
    }
}
using System;
using TMPro;
using UnityEngine;
using YARG.Core.Engine.Guitar;

namespace YARG.Menu.ScoreScreen
{
    public class GuitarScoreCard : ScoreCard<GuitarStats>
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _overstrums;

        [SerializeField]
        private TextMeshProUGUI _hoposStrummed;

        [SerializeField]
        private TextMeshProUGUI _ghostInputs;

        [SerializeField]
        private TextMeshProUGUI _starPowerActivations;

        [SerializeField]
        private TextMeshProUGUI _timeInStarPower;

        public override void SetCardContents()
        {
            base.SetCardContents();

            _overstrums.text = ColorizePrimary(Stats.Overstrums);
            _hoposStrummed.text = ColorizePrimary(Stats.HoposStrummed);
            _ghostInputs.text = ColorizePrimary(Stats.GhostInputs);

            _starPowerActivations.text = ColorizePrimary(Stats.StarPowerActivationCount);

            //@"m\:ss"
            var timeInStarPower = TimeSpan.FromSeconds(Stats.TimeInStarPower).ToString(@"m\:ss");
            _timeInStarPower.text = ColorizePrimary(timeInStarPower);
        }
    }
}

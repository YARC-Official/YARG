using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core;
using YARG.Core.Engine.Guitar;
using YARG.Helpers.Extensions;

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

        public override void SetCardContents()
        {
            base.SetCardContents();

            // We'd like to show the guitar icon to denote that the active game
            // mode is guitar, but if the chosen instrument is normally played
            // using a guitar controller, we can display that specific icon.
            // Basically only relevant when playing 5L Keys using a guitar controller.
            string iconName;
            switch (Player.Profile.CurrentInstrument)
            {
                case Instrument.FiveFretBass:
                case Instrument.FiveFretGuitar:
                case Instrument.FiveFretRhythm:
                case Instrument.FiveFretCoopGuitar:
                    iconName = Player.Profile.CurrentInstrument.ToResourceName();
                    break;
                default:
                    iconName = "guitar";
                    break;
            }

            // Set background icon
            _instrumentIcon.sprite = Addressables
                .LoadAssetAsync<Sprite>($"InstrumentIcons[{iconName}]")
                .WaitForCompletion();

            _overstrums.text = ColorizePrimary(Stats.Overstrums);
            _hoposStrummed.text = ColorizePrimary(Stats.HoposStrummed);
            _ghostInputs.text = ColorizePrimary(Stats.GhostInputs);
        }
    }
}

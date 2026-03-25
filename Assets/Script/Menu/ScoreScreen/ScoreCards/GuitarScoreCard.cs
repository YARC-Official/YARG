using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
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

        public override void SetCardContents()
        {
            base.SetCardContents();

            // Set background icon
            _instrumentIcon.sprite = Addressables
                .LoadAssetAsync<Sprite>($"InstrumentIcons[guitar]")
                .WaitForCompletion();

            _overstrums.text = ColorizePrimary(Stats.Overstrums);
            _hoposStrummed.text = ColorizePrimary(Stats.HoposStrummed);
            _ghostInputs.text = ColorizePrimary(Stats.GhostInputs);
        }
    }
}

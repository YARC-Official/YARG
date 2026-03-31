using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core;
using YARG.Core.Engine.Keys;
using YARG.Helpers.Extensions;

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

            // Similarly to the guitar card, we would want to differentiate when
            // keys are used to play a guitar/bass part, but the pro keys icon
            // looks exactly the same as 5L keys when zoomed in here anyway
            _instrumentIcon.sprite = Addressables
                .LoadAssetAsync<Sprite>($"InstrumentIcons[keys]")
                .WaitForCompletion();

            _overhits.text = ColorizePrimary(Stats.Overhits);
        }
    }
}
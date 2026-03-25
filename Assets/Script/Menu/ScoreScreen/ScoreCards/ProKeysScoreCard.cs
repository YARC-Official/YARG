using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
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

            // Set background icon
            _instrumentIcon.sprite = Addressables
                .LoadAssetAsync<Sprite>($"InstrumentIcons[keys]")
                .WaitForCompletion();

            _overhits.text = ColorizePrimary(Stats.Overhits);
        }
    }
}
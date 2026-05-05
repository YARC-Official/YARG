using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core;
using YARG.Core.Engine.Drums;
using YARG.Input;
using YARG.Localization;

namespace YARG.Menu.ScoreScreen
{
    public class DrumsScoreCard : ScoreCard<DrumsStats>
    {
        [Space]
        [SerializeField]
        private TextMeshProUGUI _overhits;
        [SerializeField]
        private StatInfo _statInfoPrefab;

        public override void SetCardContents()
        {
            base.SetCardContents();

            // Set background icon
            _instrumentIcon.sprite = Addressables
                .LoadAssetAsync<Sprite>($"InstrumentIcons[drums]")
                .WaitForCompletion();

            _overhits.text = ColorizePrimary(Stats.Overhits);

            var overhitsRow = _overhits.transform.parent;
            var bindings = BindingCollection.CreateGameplayBindings(Player.Profile.CurrentInstrument.ToNativeGameMode());

            foreach (var binding in bindings)
            {
                if (Stats.OverhitsByAction.TryGetValue(binding.Action, out int count))
                {
                    CreateOverhitRow(binding, count, overhitsRow.parent);
                }
            }
        }

        private void CreateOverhitRow(ControlBinding binding, int count, Transform parent)
        {
            var info = Instantiate(_statInfoPrefab, parent);
            string key = Player.Profile.LeftyFlip ? binding.NameLefty : binding.Name;
            info.Label.text = "<space=20px>" + Localize.Key("Bindings", key);
            info.Value.text = ColorizePrimary(count);
        }

    }
}
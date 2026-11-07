using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core;
using YARG.Core.Engine.Drums;
using YARG.Input;
using YARG.Input.Bindings;
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

            _overhits.text = ColorizePrimary(Stats.Overhits);

            var overhitsRow = _overhits.transform.parent;

            var template = ReusableBindingSetTemplates.GetTemplate(Player.Profile.CurrentInstrument.ToNativeGameMode());

            foreach (var actionInfo in template.Values)
            {
                if (Stats.OverhitsByAction.TryGetValue(actionInfo.Action, out int count))
                {
                    CreateOverhitRow(actionInfo, count, overhitsRow.parent);
                }
            }
        }

        public override Sprite GetInstrumentSprite() => Addressables
                .LoadAssetAsync<Sprite>("InstrumentIcons[drums]")
                .WaitForCompletion();

        private void CreateOverhitRow(InputActionInfo actionInfo, int count, Transform parent)
        {
            var info = Instantiate(_statInfoPrefab, parent);
            string key = Player.Profile.LeftyFlip ? actionInfo.LeftyLocalizationKey: actionInfo.Key;
            info.Label.text = "<space=20px>" + Localize.Key("Bindings", key);
            info.Value.text = ColorizePrimary(count);
        }
    }
}
using TMPro;
using UnityEngine;
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

            _overhits.text = WrapWithColor(Stats.Overhits);

            var overhitsRow = _overhits.transform.parent;
            int siblingIndex = overhitsRow.GetSiblingIndex() + 1;
            var bindings = BindingCollection.CreateGameplayBindings(Player.Profile.CurrentInstrument.ToNativeGameMode());

            foreach (var binding in bindings)
            {
                if (!Stats.OverhitsByAction.TryGetValue(binding.Action, out int count))
                {
                    continue;
                }

                CreateOverhitRow(binding, count, siblingIndex, overhitsRow.parent);
                siblingIndex++;
            }
        }

        private void CreateOverhitRow(ControlBinding binding, int count, int siblingIndex, Transform parent)
        {
            var info = Instantiate(_statInfoPrefab, parent);
            info.transform.SetSiblingIndex(siblingIndex);
            string key = Player.Profile.LeftyFlip ? binding.NameLefty : binding.Name;
            info.Label.text = "<space=20px>" + Localize.Key("Bindings", key);
            info.Value.text = WrapWithColor(count);
        }

    }
}
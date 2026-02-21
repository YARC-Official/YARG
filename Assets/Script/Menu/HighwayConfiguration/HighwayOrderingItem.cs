using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Helpers.Extensions;
using YARG.Player;
using static YARG.Core.Game.ColorProfile;

namespace YARG.Menu.HighwayConfiguration
{
    public readonly struct HighwayOrderingItemSpec<T> : IHighwayOrderingItemSpec where T : Enum
    {
        // Neither mergeable nor splittable
        // For example, Red Drum
        public HighwayOrderingItemSpec(string name, DrumsHighwayItemIconType type, int colorIndex, T enumeratedValue)
        {
            Name = name;
            Type = type;
            ColorIndex = colorIndex;
            Value = enumeratedValue;
            SplitsInto = null;
            MergesInto = null;
            MergedResult = null;
        }

        // Splittable. Provide the two items that it splits into as a tuple
        // For example, a combined Yellow splits into a Yellow Cymbal and a Yellow Tom
        public HighwayOrderingItemSpec(string name, DrumsHighwayItemIconType type, int colorIndex, T enumeratedValue, (T,T) splitsInto)
        {
            Name = name;
            Type = type;
            ColorIndex = colorIndex;
            Value = enumeratedValue;
            SplitsInto = splitsInto;
            MergesInto = null;
            MergedResult = null;
        }

        // Mergeable. Provide what it merges into, and what the merged result is.
        // For example, a Yellow Cymbal merges into a Yellow Drum to produce a combined Yellow
        public HighwayOrderingItemSpec(string name, DrumsHighwayItemIconType type, int colorIndex, T enumeratedValue, T mergesInto, T mergedResult)
        {
            Name = name;
            Type = type;
            ColorIndex = colorIndex;
            Value = enumeratedValue;
            SplitsInto = null;
            MergesInto = mergesInto;
            MergedResult = mergedResult;
        }

        public string Name { get; }
        public DrumsHighwayItemIconType Type { get; }
        public int ColorIndex { get; }
        public Enum Value { get; }
        public (Enum, Enum)? SplitsInto { get; }
        public Enum? MergesInto { get; }
        public Enum? MergedResult { get; }
    }

    public class HighwayOrderingItem : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;
        [SerializeField]
        private TextMeshProUGUI _name;
        [SerializeField]
        private Button _leftButton;
        [SerializeField]
        private Button _rightButton;
        [SerializeField]
        private Button _splitOrMergeButton;
        [SerializeField]
        private TextMeshProUGUI _splitOrMergeButtonText;

        [Space]
        [SerializeField]
        private Sprite _drumShape;
        [SerializeField]
        private Sprite _cymbalShape;
        [SerializeField]
        private Sprite _combinedShape;

        private IDrumsHighwayConfigurationMenu _configMenu;
        IHighwayOrderingItemSpec _spec;

        public void Initialize(
            IDrumsHighwayConfigurationMenu configMenu,
            IHighwayOrderingItemSpec spec,
            IFretColorProvider colorProvider,
            bool isFirst,
            bool isLast
        ) {
            _spec = spec;
            _configMenu = configMenu;
            _name.text = spec.Name;
            _icon.color = colorProvider.GetFretColor(spec.ColorIndex).ToUnityColor();
            _icon.sprite = spec.Type switch
            {
                DrumsHighwayItemIconType.Drum => _drumShape,
                DrumsHighwayItemIconType.Cymbal => _cymbalShape,
                DrumsHighwayItemIconType.Combined => _combinedShape,
                _ => throw new ArgumentOutOfRangeException("o no")
            };

            _leftButton.interactable = !isFirst;
            _rightButton.interactable = !isLast;

            if (_spec.SplitsInto is not null)
            {
                _splitOrMergeButton.gameObject.SetActive(true);
                _splitOrMergeButtonText.text = "Split";
            }
            else if (_spec.MergesInto is not null)
            {
                _splitOrMergeButton.gameObject.SetActive(true);
                _splitOrMergeButtonText.text = "Merge";
            }
            else
            {
                _splitOrMergeButton.gameObject.SetActive(false);
            }
        }

        public void MoveLeft() {
            _configMenu.MoveItemLeft(_spec.Value);
        }

        public void MoveRight() {
            _configMenu.MoveItemRight(_spec.Value);
        }

        public void SplitOrMerge()
        {
            if (_spec.MergesInto is not null)
            {
                _configMenu.MergeItemInto(_spec.Value, _spec.MergesInto, _spec.MergedResult);
            }
            else if (_spec.SplitsInto is not null)
            {
                _configMenu.SplitItemInto(_spec.Value, _spec.SplitsInto.Value);
            }
        }
    }
    public class FiveLaneDrumsHighwayOrderingItem : HighwayOrderingItem { }
}

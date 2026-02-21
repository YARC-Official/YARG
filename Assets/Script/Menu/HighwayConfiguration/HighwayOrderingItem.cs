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
        public HighwayOrderingItemSpec(string name, DrumsHighwayItemIconType type, int colorIndex, T enumeratedValue)
        {
            Name = name;
            Type = type;
            ColorIndex = colorIndex;
            Value = enumeratedValue;
        }

        public string Name { get; }
        public DrumsHighwayItemIconType Type { get; }
        public int ColorIndex { get; }
        public Enum Value { get; }
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

        [Space]
        [SerializeField]
        private Sprite _drumShape;
        [SerializeField]
        private Sprite _cymbalShape;
        [SerializeField]
        private Sprite _combinedShape;

        private IDrumsHighwayConfigurationMenu _configMenu;
        private Enum _item;

        public void Initialize(
            IDrumsHighwayConfigurationMenu configMenu,
            IHighwayOrderingItemSpec spec,
            IFretColorProvider colorProvider,
            bool isFirst,
            bool isLast
        ) {
            _item = spec.Value;
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
        }

        public void MoveLeft() {
            _configMenu.MoveItemLeft(_item);
        }

        public void MoveRight() {
            _configMenu.MoveItemRight(_item);
        }
    }
    public class FiveLaneDrumsHighwayOrderingItem : HighwayOrderingItem { }
}

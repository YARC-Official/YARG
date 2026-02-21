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
    public readonly struct HighwayOrderingItemSpec<T>
    {
        public HighwayOrderingItemSpec(string name, DrumsHighwayItemIconType type, int colorIndex, T enumeratedValue)
        {
            this.name = name;
            this.type = type;
            this.colorIndex = colorIndex;
            this.enumeratedValue = enumeratedValue;
        }

        public string name { get; }
        public DrumsHighwayItemIconType type { get; }
        public int colorIndex { get; }
        public T enumeratedValue { get; }
    }

    public abstract class HighwayOrderingItem<T> : MonoBehaviour
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

        private DrumsHighwayConfigurationMenu<T> _configMenu;
        private T _item;

        public void Initialize(DrumsHighwayConfigurationMenu<T> configMenu, HighwayOrderingItemSpec<T> spec, IFretColorProvider colorProvider, bool isFirst, bool isLast)
        {
            _item = spec.enumeratedValue;
            _configMenu = configMenu;
            _name.text = spec.name;
            _icon.color = colorProvider.GetFretColor(spec.colorIndex).ToUnityColor();
            _icon.sprite = spec.type switch
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

    public class ProDrumsHighwayOrderingItem : HighwayOrderingItem<ProDrumsHighwayItem> { }
    public class FiveLaneDrumsHighwayOrderingItem : HighwayOrderingItem<FiveLaneDrumsHighwayItem> { }
}

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
    public readonly struct HighwayOrderingItemSpec
    {
        public HighwayOrderingItemSpec(string name, DrumsHighwayItemIconType type, int colorIndex)
        {
            this.name = name;
            this.type = type;
            this.colorIndex = colorIndex;
        }

        public string name { get; }
        public DrumsHighwayItemIconType type { get; }
        public int colorIndex { get; }
    }

    public class HighwayOrderingItem : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;
        [SerializeField]
        private TextMeshProUGUI _name;

        [Space]
        [SerializeField]
        private Sprite _drumShape;
        [SerializeField]
        private Sprite _cymbalShape;
        [SerializeField]
        private Sprite _combinedShape;

        public void Initialize(HighwayOrderingItemSpec spec, IFretColorProvider colorProvider, bool isFirst, bool isLast)
        {
            _name.text = spec.name;
            _icon.color = colorProvider.GetFretColor(spec.colorIndex).ToUnityColor();
            _icon.sprite = spec.type switch
            {
                DrumsHighwayItemIconType.Drum => _drumShape,
                DrumsHighwayItemIconType.Cymbal => _cymbalShape,
                DrumsHighwayItemIconType.Combined => _combinedShape,
                _ => throw new ArgumentOutOfRangeException("o no")
            };


        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Localization;

namespace YARG.Menu.ProfileList
{
    public class BindingsIndicator : MonoBehaviour
    {
        private static Color UNLIT_COLOR = Color.gray;

        [SerializeField]
        private TextMeshProUGUI _text;
        [SerializeField]
        private Image _image;
        [SerializeField]
        private Color _color;
        [SerializeField]
        private string _localizationKey;

        public bool Lit { get; private set; }

        public void Awake()
        {
            _text.text = Localize.Key("Menu.ProfileList.BindingsIndicator", _localizationKey);
        }

        public void SetLit(bool lit)
        {
            Lit = lit;

            if (Lit)
            {
                _text.color = _color;
                _image.color = _color;
            }
            else
            {
                _text.color = UNLIT_COLOR;
                _image.color = UNLIT_COLOR;
            }
        }
    }
}

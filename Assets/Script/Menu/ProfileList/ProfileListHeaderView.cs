using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using YARG.Localization;

namespace YARG.Menu.ProfileList
{
    public class ProfileListHeaderView : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _text;

        public string Key { get; private set; }
        public string Text { get; private set; }

        public void Init(string key)
        {
            Key = key;
            Text = Localize.Key("Menu.ProfileList", key);
            _text.text = Text;
        }
    }
}

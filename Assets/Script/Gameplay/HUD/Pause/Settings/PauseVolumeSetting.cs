﻿using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Gameplay.HUD
{
    public class PauseVolumeSetting : NavigatableBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _text;
        [SerializeField]
        private Slider _slider;

        private VolumeSetting _setting;

        public void Initialize(string settingName, VolumeSetting setting)
        {
            _setting = setting;

            _text.text = Localize.Key("Settings.Setting", settingName, "PauseName");
            _slider.SetValueWithoutNotify(_setting.Value);
        }

        public void OnValueChange()
        {
            _setting.Value = _slider.value;
        }
    }
}
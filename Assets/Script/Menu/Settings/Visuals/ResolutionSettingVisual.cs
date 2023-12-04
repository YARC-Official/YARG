﻿using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Menu.Settings.Visuals
{
    public class ResolutionSettingVisual : BaseSettingVisual<ResolutionSetting>
    {
        [SerializeField]
        private TMP_Dropdown _dropdown;

        private readonly List<Resolution> _resolutionCache = new();

        protected override void RefreshVisual()
        {
            // Get the possible resolutions
            _resolutionCache.Clear();
            foreach (var resolution in Screen.resolutions)
            {
                _resolutionCache.Add(resolution);
            }

            // Add the options (in order)
            _dropdown.options.Clear();
            _dropdown.options.Add(new("<i>Highest</i>"));
            foreach (var resolution in _resolutionCache)
            {
                _dropdown.options.Add(new(resolution.ToString()));
            }

            // Select the right option
            if (Setting.Value == null)
            {
                _dropdown.SetValueWithoutNotify(0);
            }
            else
            {
                _dropdown.SetValueWithoutNotify(_resolutionCache.IndexOf(Setting.Value.Value) + 1);
            }
        }

        public override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                NavigateFinish
            }, true);
        }

        public void OnDropdownChange()
        {
            if (_dropdown.value == 0)
            {
                Setting.Value = null;
            }
            else
            {
                Setting.Value = _resolutionCache[_dropdown.value - 1];
            }

            RefreshVisual();
        }
    }
}
using Minis;
using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using YARG.Localization;

namespace YARG.Menu.ProfileList
{
    public class BindingSetsFilter : MonoBehaviour
    {
        [SerializeField]
        private TMP_Dropdown _dropdown;
        [SerializeField]
        private ProfilesMenu _profilesMenu;

        private static List<ControllerFamily> _controllerFamiliesByIndex = new()
        {
            ControllerFamily.FiveFretGuitar,
            ControllerFamily.SixFretGuitar,
            ControllerFamily.FourLaneDrumkit,
            ControllerFamily.FiveLaneDrumkit,
            ControllerFamily.ProKeyboard,
            ControllerFamily.MidiDevice,
            ControllerFamily.ProGuitar,
            ControllerFamily.Generic
        };

        public void OnEnable()
        {
            for (var i = 0; i < _controllerFamiliesByIndex.Count; i++)
            {
                _dropdown.options.Add(new(_controllerFamiliesByIndex[i].ToLocalizedName()));
            }
        }

        public void ChangeFilter()
        {
            _profilesMenu.CurrentBindingSetFilter = _controllerFamiliesByIndex[_dropdown.value];
        }
    }

    public enum ControllerFamily
    {
        FiveFretGuitar,
        SixFretGuitar,
        FourLaneDrumkit,
        FiveLaneDrumkit,
        ProKeyboard,
        MidiDevice,
        ProGuitar,
        Generic
    }
}

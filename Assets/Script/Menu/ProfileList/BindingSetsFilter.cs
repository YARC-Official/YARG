using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Localization;
using YARG.Core.Extensions;

namespace YARG.Menu.ProfileList
{
    public class BindingSetsFilter : MonoBehaviour
    {
        [SerializeField]
        private TMP_Dropdown _dropdown;
        [SerializeField]
        private ProfilesMenu _profilesMenu;

        private static ControllerFamily[] _controllerFamilies = EnumExtensions<ControllerFamily>.Values;

        public void OnEnable()
        {
            if (_dropdown.options.Count is 0)
            {
                foreach (var controllerFamily in _controllerFamilies)
                {
                    _dropdown.options.Add(new(controllerFamily.ToLocalizedNamePluralTitle()));
                }
            }
        }

        public void ChangeFilter()
        {
            var filter = _controllerFamilies[_dropdown.value];
            _profilesMenu.SetCurrentBindingSetFilter(filter);
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
        Turntable,
        Gamepad,
        ComputerKeyboard,
        Mouse,
        Other
    }
}

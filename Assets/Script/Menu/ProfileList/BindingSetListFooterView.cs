using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Input.Bindings;
using YARG.Localization;
using YARG.Menu.Dialogs;
using YARG.Menu.Persistent;

namespace YARG.Menu.ProfileList
{
    public class BindingSetListFooterView : MonoBehaviour
    {
        private static Color NOT_RECOMMENDED_BUTTON_COLOR = new(202f/255f, 199f/255f, 0f);
        private static Color RECOMMENDED_BUTTON_COLOR = new(9f/255f, 222f/255f, 123f/255f);

        [SerializeField]
        private Button _button;
        [SerializeField]
        private TextMeshProUGUI _buttonText;
        private ControllerFamily _family { get; set; }
        private List<GameMode> _remainingGameModes { get; set; }
        public ProfilesMenu ProfilesMenu { get; private set; }

        public void Init(ControllerFamily family, List<GameMode> remainingGameModes, ProfilesMenu profilesMenu, bool someExist)
        {
            _family = family;
            _remainingGameModes = remainingGameModes;
            ProfilesMenu = profilesMenu;

            if (ControllerFamilyIsFlexible)
            {
                _buttonText.text = Localize.Key(someExist ? "Menu.ProfileList.AddOtherBindingSet" : "Menu.ProfileList.AddBindingSet");
                _button.GetComponent<Image>().color = RECOMMENDED_BUTTON_COLOR;
            }
            else
            {
                _buttonText.text = Localize.Key("Menu.ProfileList.AddOtherBindingSetNotRecommended");
                _button.GetComponent<Image>().color = NOT_RECOMMENDED_BUTTON_COLOR;
            }
        }

        public void AddOther()
        {
            ListDialog dialog;

            if (_family is ControllerFamily.Other)
            {
                dialog = DialogManager.Instance.ShowList($"Add Other Binding Set\n" +
                    "<alpha=#44><size=65%>\n<b><color=\"yellow\">Note:</color></b> Because this is a binding set for miscellaneous input devices, you " +
                    "won't be able to select bindings from dropdowns. You'll need to assign a dummy controller and use the <b>Quick Bind</b> and/or " +
                    "<b>Record</b> buttons to create your binding set.</size>"
                );
            }
            else if (ControllerFamilyIsFlexible)
            {
                dialog = DialogManager.Instance.ShowList($"Add Other Binding Set");
            }
            else
            {
                dialog = DialogManager.Instance.ShowList($"Add Other {_family.ToLocalizedNameSingularTitle()} Binding Set\n" +
                    "<alpha=#44><size=65%>\n<b><color=\"yellow\">Are you sure you know what you're doing?</color></b> " +
                    $"{_family.ToLocalizedNamePluralSentence()} weren't designed with these modes in mind.</size>"
                );
            }

            foreach (var remainingGameMode in _remainingGameModes)
            {
                dialog.AddListButton(remainingGameMode.ToLocalizedName(), () =>
                {
                    var bindingSet = ReusableBindingSetTemplates.MakeBlankBindingSet(remainingGameMode, _family);
                    BindingsContainer.AddBindingSet(bindingSet);
                    ProfilesMenu.RefreshBindingSetList();
                });
            }
        }

        private bool ControllerFamilyIsFlexible => _family is
            ControllerFamily.Other or
            ControllerFamily.Gamepad or
            ControllerFamily.ComputerKeyboard or
            ControllerFamily.MidiDevice;
    }
}

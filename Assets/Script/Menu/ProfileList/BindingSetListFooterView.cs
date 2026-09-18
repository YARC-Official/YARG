using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Input.Bindings;
using YARG.Localization;
using YARG.Menu.Persistent;

namespace YARG.Menu.ProfileList
{
    public class BindingSetListFooterView : MonoBehaviour
    {
        [SerializeField]
        private Button _addOtherButton;

        private ControllerFamily _family { get; set; }
        private List<GameMode> _remainingGameModes { get; set; }
        private ProfilesMenu _profilesMenu { get; set; }

        public void Init(ControllerFamily family, List<GameMode> remainingGameModes, ProfilesMenu profilesMenu)
        {
            _family = family;
            _remainingGameModes = remainingGameModes;
            _profilesMenu = profilesMenu;
        }

        public void AddOther()
        {
            var dialog = DialogManager.Instance.ShowList($"Add Other {_family.ToLocalizedNameSingular()} Binding Set\n" +
                "<alpha=#44><size=65%>\n<b><color=\"yellow\">Are you sure you know what you're doing?</color></b> " +
                $"{_family.ToLocalizedNamePlural()} weren't designed with these modes in mind.</size>"
            );

            foreach (var remainingGameMode in _remainingGameModes)
            {
                dialog.AddListButton(remainingGameMode.ToLocalizedName(), async () =>
                {
                    var bindingSet = ReusableBindingSetTemplates.MakeBlankBindingSet(remainingGameMode, _family);
                    BindingsContainer.AddBindingSet(bindingSet);
                    _profilesMenu.RefreshBindingSetList();
                });
            }
        }
    }
}

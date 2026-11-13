using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Input.Bindings;
using YARG.Localization;

namespace YARG.Menu.ProfileList
{
    public class BindingSetListHeaderView : MonoBehaviour
    {
        [SerializeField]
        private Button _addButton;
        [SerializeField]
        private TextMeshProUGUI _text;

        public ControllerFamily Family { get; private set; }
        public GameMode Mode { get; private set; }
        private ProfilesMenu _profilesMenu;

        public void Init(ControllerFamily family, GameMode mode, ProfilesMenu profilesMenu, bool typical)
        {
            Family = family;
            Mode = mode;
            _profilesMenu = profilesMenu;

            _text.text = Localize.Key("Bindings.Headers", mode.ToString()) +
                (typical ? "" : $" <sup><i><color=\"yellow\">({Localize.Key("Bindings.Headers.NotRecommended")})</color></i></sup>");
        }

        public void AddBindingSet()
        {
            var bindingSet = ReusableBindingSetTemplates.MakeBlankBindingSet(Mode, Family);
            BindingsContainer.AddBindingSet(bindingSet);
            _profilesMenu.RefreshBindingSetList();
            _profilesMenu.SetSelectedBindingSet(bindingSet);
        }
    }
}

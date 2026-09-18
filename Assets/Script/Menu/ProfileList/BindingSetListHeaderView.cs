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

        private ControllerFamily _family { get; set; }
        private GameMode _mode { get; set; }
        private ProfilesMenu _profilesMenu { get; set; }

        public void Init(ControllerFamily family, GameMode mode, ProfilesMenu profilesMenu, bool typical)
        {
            _family = family;
            _mode = mode;
            _profilesMenu = profilesMenu;

            _text.text = Localize.Key("Bindings.Headers", mode.ToString()) +
                (typical ? "" : $" <sup><i><color=\"yellow\">({Localize.Key("Bindings.Headers.NotRecommended")})</color></i></sup>");
        }

        public void AddBindingSet()
        {
            var bindingSet = ReusableBindingSetTemplates.MakeBlankBindingSet(GetNameForNewBindingSet(), _mode, _family);
            BindingsContainer.AddBindingSet(bindingSet);
            _profilesMenu.RefreshBindingSetList();
        }

        private string GetNameForNewBindingSet()
        {
            var modeText = _mode.ToLocalizedNameShort();

            // "Typical" binding sets are things like "5F guitar on 5F guitar" or "4L drums on 4L drumkit";
            // for brevity, we skip the "on [family]" part of the naming convention
            var isTypical = (_mode, _family) is
                (GameMode.FiveFretGuitar, ControllerFamily.FiveFretGuitar) or
                (GameMode.SixFretGuitar, ControllerFamily.SixFretGuitar) or
                (GameMode.FourLaneDrums, ControllerFamily.FourLaneDrumkit) or
                (GameMode.FiveLaneDrums, ControllerFamily.FiveLaneDrumkit) or
                (GameMode.ProKeys, ControllerFamily.ProKeyboard) or
                (GameMode.ProGuitar, ControllerFamily.ProGuitar);

            if (isTypical)
            {
                var localized = Localize.KeyFormat("Bindings.Custom", modeText);
                return GetNameWithNumber(localized);
            }

            var familyText = _family switch
            {
                // If the controller is a MIDI device, use the mode to take an educated guess at what
                // kind of MIDI device we're talking about
                ControllerFamily.MidiDevice => _mode switch {
                    GameMode.ProKeys => Localize.Key("Bindings.MidiKeyboard"),
                    GameMode.FourLaneDrums or
                    GameMode.FiveLaneDrums or
                    GameMode.EliteDrums => Localize.Key("Bindings.MidiDrumkit"),
                    _ => _family.ToLocalizedNameSingular()
                },
                _ => _family.ToLocalizedNameSingular()
            };

            // Vocals bindings are rendered as "Vocals with [family]" instead of "Vocals on [family]", to
            // mitigate people thinking they can bind something to microphone pitch
            if (_mode is GameMode.Vocals)
            {
                var vocalsWithController = Localize.KeyFormat("Bindings.VocalsWithController", modeText, familyText);
                var fullVocals = Localize.KeyFormat("Bindings.Custom", vocalsWithController);
                return GetNameWithNumber(fullVocals);
            }

            var modeOnController = Localize.KeyFormat("Bindings.ModeOnController", modeText, familyText);
            var full = Localize.KeyFormat("Bindings.Custom", modeOnController);
            return GetNameWithNumber(full);
        }

        private string GetNameWithNumber(string name)
        {
            var existing = BindingsContainer.GetBindingSetsForControllerInMode(_family, _mode);

            if (existing.All(existingBindingSet => existingBindingSet.Name != name))
            {
                return name;
            }

            for (var number = 2; true; number++)
            {
                var candidateName = $"{name} #{number}";

                if (existing.All(existingBindingSet => existingBindingSet.Name != candidateName))
                {
                    return candidateName;
                }
            }
        }
    }
}

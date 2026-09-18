using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YARG.Core;
using YARG.Localization;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public enum BindingType
    {
        Button,
        IndividualButton,
        DrumButton,
        Axis,
        Integer
    }

    public struct InputActionInfo
    {
        public BindingType Type { get; }
        public int Action { get; }
        public string LocalizationKey { get; }
        public string LeftyLocalizationKey { get; }

        public InputActionInfo(string localizationKey, BindingType type, int action)
            : this(localizationKey, localizationKey, type, action) { }

        public InputActionInfo(string localizationKey, string leftyLocalizationKey, BindingType type, int action)
        {
            LocalizationKey = localizationKey;
            LeftyLocalizationKey = leftyLocalizationKey;
            Type = type;
            Action = action;
        }
    }

    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> GetTemplate(GameMode? mode)
        {
            return mode switch {
                GameMode.Menu =>            MENU,
                GameMode.FiveFretGuitar =>  FIVE_FRET_GUITAR,
                GameMode.SixFretGuitar =>   SIX_FRET_GUITAR,
                GameMode.FourLaneDrums =>   FOUR_LANE_DRUMKIT,
                GameMode.FiveLaneDrums =>   FIVE_LANE_DRUMKIT,
                GameMode.ProKeys =>         KEYS,
                GameMode.EliteDrums =>      ELITE_DRUMS,
                GameMode.Vocals =>          VOCALS,
                _ =>                        new()
            };
        }

        public static ReusableBindingSet MakeBlankBindingSet(GameMode mode, ControllerFamily family)
        {
            var name = GetNameForNewBindingSet(mode, family);
            var bindingSet = new ReusableBindingSet(name, mode, family);

            var template = GetTemplate(mode);

            foreach (var (key, info) in template)
            {
                bindingSet.Bindings[key] = info.Type switch
                {
                    BindingType.Button or BindingType.IndividualButton or BindingType.DrumButton => new ReusableButtonBinding(info),
                    BindingType.Axis => new ReusableAxisBinding(info),
                    BindingType.Integer => new ReusableIntegerBinding(info),
                    _ => throw new ArgumentOutOfRangeException("Unreachable")
                };
            }

            return bindingSet;
        }

        private static string GetNameForNewBindingSet(GameMode mode, ControllerFamily family)
        {
            var modeText = mode.ToLocalizedNameShort();

            // "Typical" binding sets are things like "5F guitar on 5F guitar" or "4L drums on 4L drumkit";
            // for brevity, we skip the "on [family]" part of the naming convention
            var isTypical = (mode, family) is
                (GameMode.FiveFretGuitar, ControllerFamily.FiveFretGuitar) or
                (GameMode.SixFretGuitar, ControllerFamily.SixFretGuitar) or
                (GameMode.FourLaneDrums, ControllerFamily.FourLaneDrumkit) or
                (GameMode.FiveLaneDrums, ControllerFamily.FiveLaneDrumkit) or
                (GameMode.ProKeys, ControllerFamily.ProKeyboard) or
                (GameMode.ProGuitar, ControllerFamily.ProGuitar);

            if (isTypical)
            {
                var localized = Localize.KeyFormat("Bindings.Custom", modeText);
                return GetNameWithNumber(localized, family, mode);
            }

            var familyText = family switch
            {
                // If the controller is a MIDI device, use the mode to take an educated guess at what
                // kind of MIDI device we're talking about
                ControllerFamily.MidiDevice => mode switch
                {
                    GameMode.ProKeys => Localize.Key("Bindings.MidiKeyboard"),
                    GameMode.FourLaneDrums or
                    GameMode.FiveLaneDrums or
                    GameMode.EliteDrums => Localize.Key("Bindings.MidiDrumkit"),
                    _ => family.ToLocalizedNameSingular()
                },
                _ => family.ToLocalizedNameSingular()
            };

            // Vocals bindings are rendered as "Vocals with [family]" instead of "Vocals on [family]", to
            // mitigate people thinking they can bind something to microphone pitch
            if (mode is GameMode.Vocals)
            {
                var vocalsWithController = Localize.KeyFormat("Bindings.VocalsWithController", modeText, familyText);
                var fullVocals = Localize.KeyFormat("Bindings.Custom", vocalsWithController);
                return GetNameWithNumber(fullVocals, family, mode);
            }

            var modeOnController = Localize.KeyFormat("Bindings.ModeOnController", modeText, familyText);
            var full = Localize.KeyFormat("Bindings.Custom", modeOnController);
            return GetNameWithNumber(full, family, mode);
        }

        private static string GetNameWithNumber(string name, ControllerFamily family, GameMode mode)
        {
            var existing = BindingsContainer.GetBindingSetsForControllerInMode(family, mode);

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

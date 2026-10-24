using System.Collections.Generic;
using System.Linq;

namespace YARG.Input.Serialization
{
    /* Legacy Binding Set Deserialization
     * When upgrading pre-BindingSerializationV4 binding sets to V4+, we don't want to create explicit binding sets for users who just
     * stuck with the defaults, even if those defaults are no longer equivalent to the current defaults. This class contains hardcoded
     * SerializedReusableBindingSets that represent the V0-3 default binding sets; when upgrading binding sets, we compare against all of
     * these to make sure we discard any legacy sets that don't reflect any explicit user choice. The new defaults are supersets of the old
     * ones, so users should never lose functionality from this discarding
     */
    public static partial class LegacyBindingSetDeserialization
    {
        public static bool LegacyBindingSetShouldBeDeserialized(SerializedReusableBindingSet candidate)
        {
            return candidate is not null && candidate.Bindings.Count is not 0 && !SerializedBindingSetIsOldDefault(candidate);
        }

        public static string IsolateControlName(string controlPath)
        {
            var secondSlashIndex = controlPath.IndexOf('/', 1);
            return controlPath.Substring(secondSlashIndex + 1);
        }

        private static bool SerializedBindingSetIsOldDefault(SerializedReusableBindingSet candidate)
        {
            foreach (var bindingSet in LegacyDefaults.All)
            {
                if (SerializedBindingSetsAreEquivalent(bindingSet, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SerializedBindingSetsAreEquivalent(SerializedReusableBindingSet a, SerializedReusableBindingSet b)
        {
            if (a.Mode != b.Mode || a.Family != b.Family || a.Bindings.Count != b.Bindings.Count)
            {
                return false;
            }

            foreach (var (key, bindingA) in a.Bindings)
            {
                if (!b.Bindings.TryGetValue(key, out var bindingB))
                {
                    return false;
                }

                foreach (var (parameterKey, parameterA) in bindingA.Parameters)
                {
                    if (!bindingB.Parameters.TryGetValue(key, out var parameterB))
                    {
                        return false;
                    }

                    if (parameterA != parameterB)
                    {
                        return false;
                    }
                }

                foreach (var singleA in bindingA.Controls)
                {
                    var singleB = bindingB.Controls.FirstOrDefault(b => singleA.ControlName == b.ControlName);

                    if (singleB is null)
                    {
                        return false;
                    }

                    if (singleA.Parameters.Count != bindingB.Parameters.Count)
                    {
                        return false;
                    }

                    foreach (var (singleParameterKey, singleParameterA) in singleA.Parameters)
                    {
                        if (!singleB.Parameters.TryGetValue(singleParameterKey, out var singleParameterB))
                        {
                            return false;
                        }

                        if (singleParameterA != singleParameterB)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        // Nested class to ensure that the list is initialized after all of its members (from other partial
        // class files) have been initialized
        private static class LegacyDefaults
        {
            internal static List<SerializedReusableBindingSet> All = new()
            {
                LEGACY_5F_GUITAR,
                LEGACY_RB_GUITAR,
                LEGACY_GH_GUITAR,
                LEGACY_RIFFMASTER,
                LEGACY_5F_GUITAR_MENU,
                LEGACY_RB_GUITAR_MENU,
                LEGACY_RIFFMASTER_MENU,

                LEGACY_6F_GUITAR,
                LEGACY_6F_GUITAR_MENU,

                LEGACY_4L_DRUMKIT,
                LEGACY_4L_DRUMKIT_MENU,

                LEGACY_5L_DRUMKIT,
                LEGACY_5L_DRUMKIT_MENU,

                LEGACY_PRO_KEYBOARD,
                LEGACY_PRO_KEYBOARD_MENU,

                LEGACY_COMPUTER_KEYBOARD_5F,
                LEGACY_COMPUTER_KEYBOARD_6F,
                LEGACY_COMPUTER_KEYBOARD_4L_DRUMS,
                LEGACY_COMPUTER_KEYBOARD_5L_DRUMS,
                LEGACY_COMPUTER_KEYBOARD_MIDI_DRUMKIT,
                LEGACY_COMPUTER_KEYBOARD_KEYS,
                LEGACY_COMPUTER_KEYBOARD_VOCALS,
                LEGACY_COMPUTER_KEYBOARD_MENU,

                LEGACY_GAMEPAD_5F,
                LEGACY_GAMEPAD_VOCALS,
                LEGACY_GAMEPAD_MENU,
                LEGACY_SWITCH_GAMEPAD_MENU,
                LEGACY_GAMEPAD_AS_5F,
                LEGACY_CRKD,
                LEGACY_CRKD_FW300,
                LEGACY_CRKD_MENU,
                LEGACY_GAMEPAD_AS_6F,
                LEGACY_GAMEPAD_AS_5F_MENU,
                LEGACY_WIITAR_THING_DRUMS,
                LEGACY_WIITAR_THING_DRUMS_MENU,
                LEGACY_RB4_INSTRUMENT_MAPPER_DRUMS,
                LEGACY_RB4_INSTRUMENT_MAPPER_DRUMS_MENU,

                LEGACY_5F_ON_PRO_GUITAR,
                LEGACY_PRO_GUITAR_MENU,
            };
        }
    }
}
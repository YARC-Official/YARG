using System.Collections.Generic;
using System.Linq;
using YARG.Core;
using YARG.Input.Bindings;
using YARG.Input.Serialization;
using YARG.Menu.ProfileList;

namespace YARG.Helpers
{
    public static class LegacyBindingSetDeserializationHelper
    {
        public static bool SerializedBindingSetIsOldDefault(SerializedReusableBindingSet candidate)
        {
            foreach (var bindingSet in _legacyDefaults)
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

        private static List<SerializedReusableBindingSet> _legacyDefaults = new()
        {
            LEGACY_5F_GUITAR,
            LEGACY_RB_GUITAR,
            LEGACY_GH_GUITAR,
            LEGACY_RIFFMASTER,
        };

        private const string DEFAULT_LEGACY_5F_TILT_PRESS_POINT = "1.0";
        private const string DEFAULT_LEGACY_RIFFMASTER_TILT_PRESS_POINT = "0.7";

        private static SerializedReusableBindingSet LEGACY_5F_GUITAR = new("Legacy Generic 5F Guitar", ControllerFamily.FiveFretGuitar, GameMode.FiveFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.FIVE_FRET_GREEN,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_GREEN_FRET,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_RED,      new() { Controls = new() { new(ControlStrings.GUITAR_5F_RED_FRET,     LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_YELLOW,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_YELLOW_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_BLUE,     new() { Controls = new() { new(ControlStrings.GUITAR_5F_BLUE_FRET,    LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_ORANGE,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_ORANGE_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STRUM_UP,    new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_UP,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_DOWN, LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_WHAMMY,      new() { Controls = new() { new(ControlStrings.GUITAR_WHAMMY,     LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() {
                    new(ControlStrings.PLASTICBAND_SELECT, LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_TILT, LayoutStrings.FIVE_FRET_GUITAR) { Parameters = new() { { "PressPoint", DEFAULT_LEGACY_5F_TILT_PRESS_POINT } } },
                } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_RB_GUITAR = new("Legacy RB Guitar", ControllerFamily.FiveFretGuitar, GameMode.FiveFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.FIVE_FRET_GREEN,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_GREEN_FRET,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_RED,      new() { Controls = new() { new(ControlStrings.GUITAR_5F_RED_FRET,     LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_YELLOW,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_YELLOW_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_BLUE,     new() { Controls = new() { new(ControlStrings.GUITAR_5F_BLUE_FRET,    LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_ORANGE,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_ORANGE_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.FIVE_FRET_SOLO_GREEN,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_SOLO_GREEN_FRET,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_RED,      new() { Controls = new() { new(ControlStrings.GUITAR_5F_SOLO_RED_FRET,     LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_YELLOW,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_SOLO_YELLOW_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_BLUE,     new() { Controls = new() { new(ControlStrings.GUITAR_5F_SOLO_BLUE_FRET,    LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_ORANGE,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_SOLO_ORANGE_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STRUM_UP,    new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_UP,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_DOWN, LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_WHAMMY,      new() { Controls = new() { new(ControlStrings.GUITAR_WHAMMY,     LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() {
                    new(ControlStrings.PLASTICBAND_SELECT, LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_TILT, LayoutStrings.FIVE_FRET_GUITAR) { Parameters = new() { { "PressPoint", DEFAULT_LEGACY_5F_TILT_PRESS_POINT } } },
                } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_GH_GUITAR = new("Legacy GH 5F Guitar", ControllerFamily.FiveFretGuitar, GameMode.FiveFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.FIVE_FRET_GREEN,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_GREEN_FRET,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_RED,      new() { Controls = new() { new(ControlStrings.GUITAR_5F_RED_FRET,     LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_YELLOW,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_YELLOW_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_BLUE,     new() { Controls = new() { new(ControlStrings.GUITAR_5F_BLUE_FRET,    LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_ORANGE,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_ORANGE_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.FIVE_FRET_SOLO_GREEN,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_TOUCH_GREEN,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_RED,      new() { Controls = new() { new(ControlStrings.GUITAR_5F_TOUCH_RED,     LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_YELLOW,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_TOUCH_YELLOW,  LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_BLUE,     new() { Controls = new() { new(ControlStrings.GUITAR_5F_TOUCH_BLUE,    LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_ORANGE,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_TOUCH_ORANGE,  LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STRUM_UP,    new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_UP,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_DOWN, LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_WHAMMY,      new() { Controls = new() { new(ControlStrings.GUITAR_WHAMMY,     LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() {
                    new(ControlStrings.PLASTICBAND_SELECT, LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_SP_PEDAL, LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_TILT, LayoutStrings.FIVE_FRET_GUITAR) { Parameters = new() { { "PressPoint", DEFAULT_LEGACY_5F_TILT_PRESS_POINT } } },
                } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_RIFFMASTER = new("Legacy Riffmaster", ControllerFamily.FiveFretGuitar, GameMode.FiveFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.FIVE_FRET_GREEN,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_GREEN_FRET,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_RED,      new() { Controls = new() { new(ControlStrings.GUITAR_5F_RED_FRET,     LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_YELLOW,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_YELLOW_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_BLUE,     new() { Controls = new() { new(ControlStrings.GUITAR_5F_BLUE_FRET,    LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_ORANGE,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_ORANGE_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STRUM_UP,    new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_UP,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_DOWN, LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_WHAMMY,      new() { Controls = new() { new(ControlStrings.GUITAR_WHAMMY,     LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() {
                    new(ControlStrings.PLASTICBAND_SELECT, LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_TILT, LayoutStrings.FIVE_FRET_GUITAR) { Parameters = new() { { "PressPoint", DEFAULT_LEGACY_RIFFMASTER_TILT_PRESS_POINT } } },
                } } },
            }
        };
    }
}
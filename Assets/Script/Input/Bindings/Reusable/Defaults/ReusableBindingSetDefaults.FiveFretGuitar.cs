using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;
using YARG.Input.Bindings;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _fiveFretGuitarDefaults = new()
        {
            {
                ControlStrings.FIVE_FRET_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.greenFret))
                )
            },
            {
                ControlStrings.FIVE_FRET_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.redFret))
                )
            },
            {
                ControlStrings.FIVE_FRET_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.yellowFret))
                )
            },
            {
                ControlStrings.FIVE_FRET_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.blueFret))
                )
            },
            {
                ControlStrings.FIVE_FRET_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.orangeFret))
                )
            },

            {
                ControlStrings.GUITAR_STRUM_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STRUM_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, ControlStrings.DPAD_DOWN)
                )
            },
            {
                ControlStrings.GUITAR_STRUM_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STRUM_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, ControlStrings.DPAD_UP)
                )
            },

            {
                ControlStrings.GUITAR_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STAR_POWER],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.tilt)) { PressPoint = 1f },
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.selectButton)),
                        new(ControllerFamily.FiveFretGuitar, nameof(GuitarHeroGuitar.spPedal)),
                    }
                )
            },

            {
                ControlStrings.GUITAR_WHAMMY,
                new ReusableAxisBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_WHAMMY],
                    new ReusableSingleAxisBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.whammy))
                )
            },

            {
                ControlStrings.FIVE_FRET_SOLO_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloGreen))
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloRed))
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloYellow))
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloBlue))
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloOrange))
                )
            },

        };

        // This differs from the regular default only in its tilt PressPoint calibration parameter
        // TODO: Single-source the shared configurations between these two so they aren't giant copy-pastes of each other
        private static Dictionary<string, ReusableControlBinding> _riffmasterGuitarDefaults = new()
        {
            {
                ControlStrings.FIVE_FRET_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.greenFret))
                )
            },
            {
                ControlStrings.FIVE_FRET_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.redFret))
                )
            },
            {
                ControlStrings.FIVE_FRET_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.yellowFret))
                )
            },
            {
                ControlStrings.FIVE_FRET_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.blueFret))
                )
            },
            {
                ControlStrings.FIVE_FRET_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.orangeFret))
                )
            },

            {
                ControlStrings.GUITAR_STRUM_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STRUM_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.strumDown))
                )
            },
            {
                ControlStrings.GUITAR_STRUM_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STRUM_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.strumUp))
                )
            },

            {
                ControlStrings.GUITAR_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STAR_POWER],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.tilt)) { PressPoint = 0.7f },
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.selectButton)),
                        new(ControllerFamily.FiveFretGuitar, nameof(GuitarHeroGuitar.spPedal)),
                    }
                )
            },

            {
                ControlStrings.GUITAR_WHAMMY,
                new ReusableAxisBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_WHAMMY],
                    new ReusableSingleAxisBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.whammy))
                )
            },

            {
                ControlStrings.FIVE_FRET_SOLO_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloGreen))
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloRed))
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloYellow))
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloBlue))
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloOrange))
                )
            },

        };


        private static Dictionary<string, ReusableControlBinding> _fiveFretGuitarMenuDefaults = new()
        {
            {
                ControlStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.startButton))
                )
            },
            {
                ControlStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.selectButton))
                )
            },

            {
                ControlStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_GREEN],
                    new List<ReusableSingleButtonBindingConfig> () {
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.greenFret)),
                        new(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloGreen)),
                        new(ControllerFamily.FiveFretGuitar, nameof(GuitarHeroGuitar.touchGreen)),
                    }
                )
            },
            {
                ControlStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RED],
                    new List<ReusableSingleButtonBindingConfig> () {
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.redFret)),
                        new(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloRed)),
                        new(ControllerFamily.FiveFretGuitar, nameof(GuitarHeroGuitar.touchRed)),
                    }
                )
            },
            {
                ControlStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_YELLOW],
                    new List<ReusableSingleButtonBindingConfig> () {
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.yellowFret)),
                        new(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloYellow)),
                        new(ControllerFamily.FiveFretGuitar, nameof(GuitarHeroGuitar.touchYellow)),
                    }
                )
            },
            {
                ControlStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_BLUE],
                    new List<ReusableSingleButtonBindingConfig> () {
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.blueFret)),
                        new(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloBlue)),
                        new(ControllerFamily.FiveFretGuitar, nameof(GuitarHeroGuitar.touchBlue)),
                    }
                )
            },
            {
                ControlStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_ORANGE],
                    new List<ReusableSingleButtonBindingConfig> () {
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.orangeFret)),
                        new(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloOrange)),
                        new(ControllerFamily.FiveFretGuitar, nameof(GuitarHeroGuitar.touchOrange)),
                    }
                )
            },

            {
                ControlStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_UP],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FiveFretGuitar, ControlStrings.DPAD_UP),
                        new (ControllerFamily.FiveFretGuitar, ControlStrings.JOYSTICK_UP)
                    }
                )
            },
            {
                ControlStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_DOWN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FiveFretGuitar, ControlStrings.DPAD_DOWN),
                        new (ControllerFamily.FiveFretGuitar, ControlStrings.JOYSTICK_DOWN)
                    }
                )
            },

                        {
                ControlStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_UP],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FiveFretGuitar, ControlStrings.DPAD_LEFT),
                        new (ControllerFamily.FiveFretGuitar, ControlStrings.JOYSTICK_LEFT)
                    }
                )
            },
            {
                ControlStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_DOWN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FiveFretGuitar, ControlStrings.DPAD_RIGHT),
                        new (ControllerFamily.FiveFretGuitar, ControlStrings.JOYSTICK_RIGHT)
                    }
                )
            },
        };


        public static ReusableBindingSet DefaultFiveFretGuitar = MakeHardcodedBindingSet(
            "Default 5F Gameplay",
            GameMode.FiveFretGuitar,
            ControllerFamily.FiveFretGuitar,
            _fiveFretGuitarDefaults
        );

        public static ReusableBindingSet DefaultRiffmasterGuitar = MakeHardcodedBindingSet(
            "Default Riffmaster",
            GameMode.FiveFretGuitar,
            ControllerFamily.FiveFretGuitar,
            _riffmasterGuitarDefaults
        );

        public static ReusableBindingSet DefaultFiveFretGuitarMenu = MakeHardcodedBindingSet(
            "Default 5F Menu",
            GameMode.Menu,
            ControllerFamily.FiveFretGuitar,
            _fiveFretGuitarMenuDefaults
        );
    }
}

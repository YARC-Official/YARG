using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private const float DEFAULT_TILT_PRESS_POINT = 1f;
        private const float DEFAULT_RIFFMASTER_TILT_PRESS_POINT = 0.7f;

        private static Dictionary<string, ReusableControlBinding> _fiveFretGuitarDefaults = new()
        {
            {
                ActionStrings.FIVE_FRET_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.greenFret))
                )
            },
            {
                ActionStrings.FIVE_FRET_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.redFret))
                )
            },
            {
                ActionStrings.FIVE_FRET_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.yellowFret))
                )
            },
            {
                ActionStrings.FIVE_FRET_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.blueFret))
                )
            },
            {
                ActionStrings.FIVE_FRET_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.orangeFret))
                )
            },

            {
                ActionStrings.GUITAR_STRUM_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.GUITAR_STRUM_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.strumDown))
                )
            },
            {
                ActionStrings.GUITAR_STRUM_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.GUITAR_STRUM_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.strumUp))
                )
            },

            {
                ActionStrings.GUITAR_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.GUITAR_STAR_POWER],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.tilt)) { PressPoint = DEFAULT_TILT_PRESS_POINT },
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.selectButton)),
                        new(ControllerFamily.FiveFretGuitar, nameof(GuitarHeroGuitar.spPedal)),
                    }
                )
            },

            {
                ActionStrings.GUITAR_WHAMMY,
                new ReusableAxisBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.GUITAR_WHAMMY],
                    new ReusableSingleAxisBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.whammy))
                )
            },

            {
                ActionStrings.FIVE_FRET_SOLO_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_SOLO_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloGreen))
                )
            },
            {
                ActionStrings.FIVE_FRET_SOLO_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_SOLO_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloRed))
                )
            },
            {
                ActionStrings.FIVE_FRET_SOLO_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_SOLO_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloYellow))
                )
            },
            {
                ActionStrings.FIVE_FRET_SOLO_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_SOLO_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloBlue))
                )
            },
            {
                ActionStrings.FIVE_FRET_SOLO_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_SOLO_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloOrange))
                )
            },

        };

        // This differs from the regular default only in its tilt PressPoint calibration parameter
        // TODO: Single-source the shared configurations between these two so they aren't giant copy-pastes of each other
        private static Dictionary<string, ReusableControlBinding> _riffmasterGuitarDefaults = new()
        {
            {
                ActionStrings.FIVE_FRET_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.greenFret))
                )
            },
            {
                ActionStrings.FIVE_FRET_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.redFret))
                )
            },
            {
                ActionStrings.FIVE_FRET_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.yellowFret))
                )
            },
            {
                ActionStrings.FIVE_FRET_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.blueFret))
                )
            },
            {
                ActionStrings.FIVE_FRET_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.orangeFret))
                )
            },

            {
                ActionStrings.GUITAR_STRUM_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.GUITAR_STRUM_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.strumDown))
                )
            },
            {
                ActionStrings.GUITAR_STRUM_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.GUITAR_STRUM_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.strumUp))
                )
            },

            {
                ActionStrings.GUITAR_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.GUITAR_STAR_POWER],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.tilt)) { PressPoint = DEFAULT_RIFFMASTER_TILT_PRESS_POINT },
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.selectButton)),
                        new(ControllerFamily.FiveFretGuitar, nameof(GuitarHeroGuitar.spPedal)),
                    }
                )
            },

            {
                ActionStrings.GUITAR_WHAMMY,
                new ReusableAxisBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.GUITAR_WHAMMY],
                    new ReusableSingleAxisBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.whammy))
                )
            },

            {
                ActionStrings.FIVE_FRET_SOLO_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_SOLO_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloGreen))
                )
            },
            {
                ActionStrings.FIVE_FRET_SOLO_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_SOLO_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloRed))
                )
            },
            {
                ActionStrings.FIVE_FRET_SOLO_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_SOLO_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloYellow))
                )
            },
            {
                ActionStrings.FIVE_FRET_SOLO_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_SOLO_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloBlue))
                )
            },
            {
                ActionStrings.FIVE_FRET_SOLO_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_SOLO_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloOrange))
                )
            },
        };

        private static Dictionary<string, ReusableControlBinding> _fiveFretGuitarMenuDefaults = new()
        {
            {
                ActionStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.startButton))
                )
            },
            {
                ActionStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.selectButton))
                )
            },

            {
                ActionStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_GREEN],
                    new List<ReusableSingleButtonBindingConfig> () {
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.greenFret)),
                        new(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloGreen)),
                        new(ControllerFamily.FiveFretGuitar, nameof(GuitarHeroGuitar.touchGreen)),
                    }
                )
            },
            {
                ActionStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RED],
                    new List<ReusableSingleButtonBindingConfig> () {
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.redFret)),
                        new(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloRed)),
                        new(ControllerFamily.FiveFretGuitar, nameof(GuitarHeroGuitar.touchRed)),
                    }
                )
            },
            {
                ActionStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_YELLOW],
                    new List<ReusableSingleButtonBindingConfig> () {
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.yellowFret)),
                        new(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloYellow)),
                        new(ControllerFamily.FiveFretGuitar, nameof(GuitarHeroGuitar.touchYellow)),
                    }
                )
            },
            {
                ActionStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_BLUE],
                    new List<ReusableSingleButtonBindingConfig> () {
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.blueFret)),
                        new(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloBlue)),
                        new(ControllerFamily.FiveFretGuitar, nameof(GuitarHeroGuitar.touchBlue)),
                    }
                )
            },
            {
                ActionStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_ORANGE],
                    new List<ReusableSingleButtonBindingConfig> () {
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.orangeFret)),
                        new(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloOrange)),
                        new(ControllerFamily.FiveFretGuitar, nameof(GuitarHeroGuitar.touchOrange)),
                    }
                )
            },

            {
                ActionStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_UP],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FiveFretGuitar, ControlStrings.GAMEPAD_DPAD_UP),
                        new (ControllerFamily.FiveFretGuitar, ControlStrings.JOYSTICK_UP)
                    }
                )
            },
            {
                ActionStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_DOWN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FiveFretGuitar, ControlStrings.GAMEPAD_DPAD_DOWN),
                        new (ControllerFamily.FiveFretGuitar, ControlStrings.JOYSTICK_DOWN)
                    }
                )
            },

            {
                ActionStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_LEFT],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FiveFretGuitar, ControlStrings.GAMEPAD_DPAD_LEFT),
                        new (ControllerFamily.FiveFretGuitar, ControlStrings.JOYSTICK_LEFT)
                    }
                )
            },
            {
                ActionStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RIGHT],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FiveFretGuitar, ControlStrings.GAMEPAD_DPAD_RIGHT),
                        new (ControllerFamily.FiveFretGuitar, ControlStrings.JOYSTICK_RIGHT)
                    }
                )
            },
        };


        public static ReusableBindingSet DefaultFiveFretGuitar = MakeHardcodedBindingSet(
            "Default 5F Guitar",
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
            "Default 5F Guitar Menu",
            GameMode.Menu,
            ControllerFamily.FiveFretGuitar,
            _fiveFretGuitarMenuDefaults
        );
    }
}

using Minis;
using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;

namespace YARG.Input.Bindings
{
    public static class LayoutStrings
    {
        public const string ANY = nameof(InputDevice);

        public const string MOUSE = nameof(Mouse);
        public const string KEYBOARD = nameof(Keyboard);
        public const string GAMEPAD = nameof(Gamepad);
        public const string MIDI_DEVICE = nameof(MidiDevice);

        public const string FIVE_FRET_GUITAR = nameof(FiveFretGuitar);
        public const string GUITAR_HERO_GUITAR = nameof(GuitarHeroGuitar);
        public const string PS3_GUITAR_HERO_GUITAR = "PS3GuitarHeroGuitar";
        public const string SANTROLLER_HID_GUITAR_HERO_GUITAR = "SantrollerHIDGuitarHeroGuitar";
        public const string SANTROLLER_XINPUT_GUITAR_HERO_GUITAR = "SantrollerXInputGuitarHeroGuitar";
        public const string XINPUT_GUITAR_HERO_GUITAR = "XInputGuitarHeroGuitar";
        public const string RIFFMASTER_GUITAR = nameof(RiffmasterGuitar);
        public const string PS4_RIFFMASTER_GUITAR = "PS4RiffmasterGuitar";
        public const string PS5_RIFFMASTER_GUITAR = "PS5RiffmasterGuitar";
        public const string XBOX_ONE_RIFFMASTER_GUITAR = "XboxOneRiffmasterGuitar";
        public const string ROCK_BAND_GUITAR = nameof(RockBandGuitar);
        public const string PS3_ROCK_BAND_GUITAR = "PS3RockBandGuitar";
        public const string PS4_ROCK_BAND_GUITAR = "PS4RockBandGuitar";
        public const string SANTROLLER_HID_ROCK_BAND_GUITAR = "SantrollerHIDRockBandGuitar";
        public const string SANTROLLER_XINPUT_ROCK_BAND_GUITAR = "SantrollerXInputRockBandGuitar";
        public const string WII_ROCK_BAND_GUITAR = "WiiRockBandGuitar";
        public const string XINPUT_ROCK_BAND_GUITAR = "XInputRockBandGuitar";
        public const string XBOX_ONE_ROCK_BAND_GUITAR = "XboxOneRockBandGuitar";
        public const string GUITAR_PRAISE_GUITAR = "GuitarPraiseGuitar";


        public const string SIX_FRET_GUITAR = nameof(SixFretGuitar);
        public const string PS3_WII_U_SIX_FRET_GUITAR = "PS3WiiUSixFretGuitar";
        public const string PS4_SIX_FRET_GUITAR = "PS4SixFretGuitar";
        public const string SANTROLLER_HID_SIX_FRET_GUITAR = "SantrollerHIDSixFretGuitar";
        public const string SANTROLLER_XINPUT_SIX_FRET_GUITAR = "SantrollerXInputSixFretGuitar";
        public const string XINPUT_SIX_FRET_GUITAR = "XInputSixFretGuitar";
        public const string XBOX_ONE_SIX_FRET_GUITAR = "XboxOneSixFretGuitar";


        public const string FOUR_LANE_DRUMKIT = nameof(FourLaneDrumkit);
        public const string PS3_FOUR_LANE_DRUMKIT = "PS3FourLaneDrumkit";
        public const string PS4_FOUR_LANE_DRUMKIT = "PS4FourLaneDrumkit";
        public const string SANTROLLER_HID_FOUR_LANE_DRUMKIT = "SantrollerHIDFourLaneDrumkit";
        public const string SANTROLLER_XINPUT_FOUR_LANE_DRUMKIT = "SantrollerXInputFourLaneDrumkit";
        public const string WII_FOUR_LANE_DRUMKIT = "WiiFourLaneDrumkit";
        public const string XINPUT_FOUR_LANE_DRUMKIT = "XInputFourLaneDrumkit";
        public const string XBOX_ONE_FOUR_LANE_DRUMKIT = "XboxOneFourLaneDrumkit";


        public const string FIVE_LANE_DRUMKIT = nameof(FiveLaneDrumkit);
        public const string PS3_FIVE_LANE_DRUMKIT = "PS3FiveLaneDrumkit";
        public const string SANTROLLER_HID_FIVE_LANE_DRUMKIT = "SantrollerHIDFiveLaneDrumkit";
        public const string SANTROLLER_XINPUT_FIVE_LANE_DRUMKIT = "SantrollerXInputFiveLaneDrumkit";
        public const string XINPUT_FIVE_LANE_DRUMKIT = "XInputFiveLaneDrumkit";


        public const string PRO_KEYBOARD = nameof(ProKeyboard);
        public const string PS3_PRO_KEYBOARD = "PS3ProKeyboard";
        public const string WII_PRO_KEYBOARD = "WiiProKeyboard";
        public const string XINPUT_PRO_KEYBOARD = "XInputProKeyboard";

        public const string PRO_GUITAR = nameof(ProGuitar);


        public static string GetBaseLayout(string layout)
        {
            return layout switch
            {
                GUITAR_HERO_GUITAR or
                PS3_GUITAR_HERO_GUITAR or
                SANTROLLER_HID_GUITAR_HERO_GUITAR or
                SANTROLLER_XINPUT_GUITAR_HERO_GUITAR or
                XINPUT_GUITAR_HERO_GUITAR or
                RIFFMASTER_GUITAR or
                PS4_RIFFMASTER_GUITAR or
                PS5_RIFFMASTER_GUITAR or
                XBOX_ONE_RIFFMASTER_GUITAR or
                ROCK_BAND_GUITAR or
                PS3_ROCK_BAND_GUITAR or
                PS4_ROCK_BAND_GUITAR or
                SANTROLLER_HID_ROCK_BAND_GUITAR or
                SANTROLLER_XINPUT_ROCK_BAND_GUITAR or
                WII_ROCK_BAND_GUITAR or
                XINPUT_ROCK_BAND_GUITAR or
                XBOX_ONE_ROCK_BAND_GUITAR or
                GUITAR_PRAISE_GUITAR => FIVE_FRET_GUITAR,

                PS3_WII_U_SIX_FRET_GUITAR or
                PS4_SIX_FRET_GUITAR or
                SANTROLLER_HID_SIX_FRET_GUITAR or
                SANTROLLER_XINPUT_SIX_FRET_GUITAR or
                XINPUT_SIX_FRET_GUITAR or
                XBOX_ONE_SIX_FRET_GUITAR => SIX_FRET_GUITAR,

                PS3_FOUR_LANE_DRUMKIT or
                PS4_FOUR_LANE_DRUMKIT or
                SANTROLLER_HID_FOUR_LANE_DRUMKIT or
                SANTROLLER_XINPUT_FOUR_LANE_DRUMKIT or
                WII_FOUR_LANE_DRUMKIT or
                XINPUT_FOUR_LANE_DRUMKIT or
                XBOX_ONE_FOUR_LANE_DRUMKIT => FOUR_LANE_DRUMKIT,

                PS3_FIVE_LANE_DRUMKIT or
                SANTROLLER_HID_FIVE_LANE_DRUMKIT or
                SANTROLLER_XINPUT_FIVE_LANE_DRUMKIT or
                XINPUT_FIVE_LANE_DRUMKIT => FIVE_LANE_DRUMKIT,

                PS3_PRO_KEYBOARD or
                WII_PRO_KEYBOARD or 
                XINPUT_PRO_KEYBOARD => PRO_KEYBOARD,

                _ => layout
            };
        }
    }
}

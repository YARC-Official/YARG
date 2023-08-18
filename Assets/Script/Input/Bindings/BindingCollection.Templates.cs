using System;
using YARG.Core;
using YARG.Core.Input;

namespace YARG.Input
{
    public partial class BindingCollection
    {
        public static BindingCollection CreateMenuBindings() => new()
        {
            new ButtonBinding("menu_Start",  (int) MenuAction.Start),
            new ButtonBinding("menu_Select", (int) MenuAction.Select),

            new ButtonBinding("menu_Green",  (int) MenuAction.Green),
            new ButtonBinding("menu_Red",    (int) MenuAction.Red),
            new ButtonBinding("menu_Yellow", (int) MenuAction.Yellow),
            new ButtonBinding("menu_Blue",   (int) MenuAction.Blue),
            new ButtonBinding("menu_Orange", (int) MenuAction.Orange),

            new ButtonBinding("menu_Up",    (int) MenuAction.Up),
            new ButtonBinding("menu_Down",  (int) MenuAction.Down),
            new ButtonBinding("menu_Left",  (int) MenuAction.Left),
            new ButtonBinding("menu_Right", (int) MenuAction.Right),
        };

        public static BindingCollection CreateFiveFretGuitarBindings() => new()
        {
            new ButtonBinding("fiveFret_Green",  (int) GuitarAction.GreenFret),
            new ButtonBinding("fiveFret_Red",    (int) GuitarAction.RedFret),
            new ButtonBinding("fiveFret_Yellow", (int) GuitarAction.YellowFret),
            new ButtonBinding("fiveFret_Blue",   (int) GuitarAction.BlueFret),
            new ButtonBinding("fiveFret_Orange", (int) GuitarAction.OrangeFret),

            new ButtonBinding("guitar_StrumUp",   (int) GuitarAction.StrumUp),
            new ButtonBinding("guitar_StrumDown", (int) GuitarAction.StrumDown),

            new ButtonBinding("guitar_StarPower", (int) GuitarAction.StarPower),

            new AxisBinding("guitar_Whammy", (int) GuitarAction.Whammy),
        };

        public static BindingCollection CreateSixFretGuitarBindings() => new()
        {
            new ButtonBinding("sixFret_Black1", (int) GuitarAction.Black1Fret),
            new ButtonBinding("sixFret_Black2", (int) GuitarAction.Black2Fret),
            new ButtonBinding("sixFret_Black3", (int) GuitarAction.Black3Fret),
            new ButtonBinding("sixFret_White1", (int) GuitarAction.White1Fret),
            new ButtonBinding("sixFret_White2", (int) GuitarAction.White2Fret),
            new ButtonBinding("sixFret_White3", (int) GuitarAction.White3Fret),

            new ButtonBinding("guitar_StrumUp",   (int) GuitarAction.StrumUp),
            new ButtonBinding("guitar_StrumDown", (int) GuitarAction.StrumDown),

            new ButtonBinding("guitar_StarPower", (int) GuitarAction.StarPower),

            new AxisBinding("guitar_Whammy", (int) GuitarAction.Whammy),
        };

        public static BindingCollection CreateFourLaneDrumsBindings() => new()
        {
            new ButtonBinding("drums_RedPad",    (int) DrumsAction.RedDrum),
            new ButtonBinding("drums_YellowPad", (int) DrumsAction.YellowDrum),
            new ButtonBinding("drums_BluePad",   (int) DrumsAction.BlueDrum),
            new ButtonBinding("drums_GreenPad",  (int) DrumsAction.GreenDrum),

            new ButtonBinding("drums_YellowCymbal", (int) DrumsAction.YellowCymbal),
            new ButtonBinding("drums_BlueCymbal",   (int) DrumsAction.BlueCymbal),
            new ButtonBinding("drums_GreenCymbal",  (int) DrumsAction.GreenCymbal),

            new ButtonBinding("drums_Kick", (int) DrumsAction.Kick),
        };

        public static BindingCollection CreateFiveLaneDrumsBindings() => new()
        {
            new ButtonBinding("drums_RedPad",       (int) DrumsAction.RedDrum),
            new ButtonBinding("drums_YellowCymbal", (int) DrumsAction.YellowCymbal),
            new ButtonBinding("drums_BluePad",      (int) DrumsAction.BlueDrum),
            new ButtonBinding("drums_OrangeCymbal", (int) DrumsAction.OrangeCymbal),
            new ButtonBinding("drums_GreenPad",     (int) DrumsAction.GreenDrum),

            new ButtonBinding("drums_Kick", (int) DrumsAction.Kick),
        };

        public static BindingCollection CreateProGuitarBindings() => new()
        {
            new IntegerBinding("proGuitar_String1_Fret", (int) ProGuitarAction.String1_Fret),
            new IntegerBinding("proGuitar_String2_Fret", (int) ProGuitarAction.String2_Fret),
            new IntegerBinding("proGuitar_String3_Fret", (int) ProGuitarAction.String3_Fret),
            new IntegerBinding("proGuitar_String4_Fret", (int) ProGuitarAction.String4_Fret),
            new IntegerBinding("proGuitar_String5_Fret", (int) ProGuitarAction.String5_Fret),
            new IntegerBinding("proGuitar_String6_Fret", (int) ProGuitarAction.String6_Fret),

            new ButtonBinding("proGuitar_String1_Strum", (int) ProGuitarAction.String1_Strum),
            new ButtonBinding("proGuitar_String2_Strum", (int) ProGuitarAction.String2_Strum),
            new ButtonBinding("proGuitar_String3_Strum", (int) ProGuitarAction.String3_Strum),
            new ButtonBinding("proGuitar_String4_Strum", (int) ProGuitarAction.String4_Strum),
            new ButtonBinding("proGuitar_String5_Strum", (int) ProGuitarAction.String5_Strum),
            new ButtonBinding("proGuitar_String6_Strum", (int) ProGuitarAction.String6_Strum),

            new ButtonBinding("guitar_StarPower", (int) ProGuitarAction.StarPower),

            new AxisBinding("guitar_Whammy", (int) ProGuitarAction.Whammy),
        };

        public static BindingCollection CreateProKeysBindings() => new()
        {
            // new ButtonBinding("proKeys_Key1",  (int) ProKeysAction.Key1),
            // new ButtonBinding("proKeys_Key2",  (int) ProKeysAction.Key2),
            // new ButtonBinding("proKeys_Key3",  (int) ProKeysAction.Key3),
            // new ButtonBinding("proKeys_Key4",  (int) ProKeysAction.Key4),
            // new ButtonBinding("proKeys_Key5",  (int) ProKeysAction.Key5),

            // new ButtonBinding("proKeys_Key6",  (int) ProKeysAction.Key6),
            // new ButtonBinding("proKeys_Key7",  (int) ProKeysAction.Key7),
            // new ButtonBinding("proKeys_Key8",  (int) ProKeysAction.Key8),
            // new ButtonBinding("proKeys_Key9",  (int) ProKeysAction.Key9),
            // new ButtonBinding("proKeys_Key10", (int) ProKeysAction.Key10),
            // new ButtonBinding("proKeys_Key11", (int) ProKeysAction.Key11),
            // new ButtonBinding("proKeys_Key12", (int) ProKeysAction.Key12),

            // new ButtonBinding("proKeys_Key13", (int) ProKeysAction.Key13),
            // new ButtonBinding("proKeys_Key14", (int) ProKeysAction.Key14),
            // new ButtonBinding("proKeys_Key15", (int) ProKeysAction.Key15),
            // new ButtonBinding("proKeys_Key16", (int) ProKeysAction.Key16),
            // new ButtonBinding("proKeys_Key17", (int) ProKeysAction.Key17),

            // new ButtonBinding("proKeys_Key18", (int) ProKeysAction.Key18),
            // new ButtonBinding("proKeys_Key19", (int) ProKeysAction.Key19),
            // new ButtonBinding("proKeys_Key20", (int) ProKeysAction.Key20),
            // new ButtonBinding("proKeys_Key21", (int) ProKeysAction.Key21),
            // new ButtonBinding("proKeys_Key22", (int) ProKeysAction.Key22),
            // new ButtonBinding("proKeys_Key23", (int) ProKeysAction.Key23),
            // new ButtonBinding("proKeys_Key24", (int) ProKeysAction.Key24),

            // new ButtonBinding("proKeys_Key25", (int) ProKeysAction.Key25),

            // new ButtonBinding("proKeys_StarPower", (int) ProKeysAction.StarPower),

            // new AxisBinding("proKeys_TouchEffects", (int) ProKeysAction.TouchEffects),
        };

        public static BindingCollection CreateVocalsBindings() => new()
        {
            // Only needed if we want to over-do it and run audio devices
            // through the Unity input system (which actually wouldn't be that hard lol)
            // new AxisBinding("vocals_Pitch", (int) VocalsAction.Pitch),

            new ButtonBinding("vocals_StarPower", (int) VocalsAction.StarPower),
        };

        public static BindingCollection CreateGameplayBindings(GameMode mode)
        {
            return mode switch
            {
                GameMode.FiveFretGuitar => CreateFiveFretGuitarBindings(),
                GameMode.SixFretGuitar => CreateSixFretGuitarBindings(),

                GameMode.FourLaneDrums => CreateFourLaneDrumsBindings(),
                GameMode.FiveLaneDrums => CreateFiveLaneDrumsBindings(),

                GameMode.ProGuitar => CreateProGuitarBindings(),
                GameMode.ProKeys => CreateProKeysBindings(),

                GameMode.Vocals => CreateVocalsBindings(),

                _ => throw new NotImplementedException($"Unhandled game mode {mode}!")
            };
        }
    }
}
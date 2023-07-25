using System;
using YARG.Core;
using YARG.Core.Input;

namespace YARG.Input
{
    public partial class GameModeBindings
    {
        public static GameModeBindings CreateMenuBindings() => new()
        {
            new ButtonBinding("menu_Green",  (int) MenuAction.Green),
            new ButtonBinding("menu_Red",    (int) MenuAction.Red),
            new ButtonBinding("menu_Yellow", (int) MenuAction.Yellow),
            new ButtonBinding("menu_Blue",   (int) MenuAction.Blue),
            new ButtonBinding("menu_Orange", (int) MenuAction.Orange),

            new ButtonBinding("menu_Up",    (int) MenuAction.Up),
            new ButtonBinding("menu_Down",  (int) MenuAction.Down),
            new ButtonBinding("menu_Left",  (int) MenuAction.Left),
            new ButtonBinding("menu_Right", (int) MenuAction.Right),

            new ButtonBinding("menu_Start",  (int) MenuAction.Start),
            new ButtonBinding("menu_Select", (int) MenuAction.Select),
        };

        public static GameModeBindings CreateFiveFretGuitarBindings() => new()
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

        public static GameModeBindings CreateSixFretGuitarBindings() => new()
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

        public static GameModeBindings CreateProGuitarBindings() => new()
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

        public static GameModeBindings CreateFourLaneDrumsBindings() => new()
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

        public static GameModeBindings CreateFiveLaneDrumsBindings() => new()
        {
            new ButtonBinding("drums_RedPad",       (int) DrumsAction.RedDrum),
            new ButtonBinding("drums_YellowCymbal", (int) DrumsAction.YellowCymbal),
            new ButtonBinding("drums_BluePad",      (int) DrumsAction.BlueDrum),
            new ButtonBinding("drums_OrangeCymbal", (int) DrumsAction.OrangeCymbal),
            new ButtonBinding("drums_GreenPad",     (int) DrumsAction.GreenDrum),

            new ButtonBinding("drums_Kick", (int) DrumsAction.Kick),
        };

        public static GameModeBindings CreateVocalsBindings() => new()
        {
            // Only needed if we want to over-do it and run audio devices
            // through the Unity input system (which actually wouldn't be that hard lol)
            // new AxisBinding("vocals_Pitch", (int) VocalsAction.Pitch),

            new ButtonBinding("vocals_StarPower", (int) VocalsAction.StarPower),
        };

        public static GameModeBindings CreateBindingsForGameMode(GameMode mode)
        {
            return mode switch
            {
                GameMode.FiveFretGuitar => CreateFiveFretGuitarBindings(),
                GameMode.SixFretGuitar => CreateSixFretGuitarBindings(),

                GameMode.FourLaneDrums => CreateFourLaneDrumsBindings(),
                GameMode.FiveLaneDrums => CreateFiveLaneDrumsBindings(),

                GameMode.ProGuitar => CreateProGuitarBindings(),
                // GameMode.ProKeys => CreateProKeysBindings(),

                GameMode.Vocals => CreateVocalsBindings(),

                _ => throw new NotImplementedException($"Unhandled game mode {mode}!")
            };
        }
    }
}
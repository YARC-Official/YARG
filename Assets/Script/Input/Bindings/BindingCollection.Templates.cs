using System;
using YARG.Core;
using YARG.Core.Input;

namespace YARG.Input
{
    public partial class BindingCollection
    {
        public static BindingCollection CreateMenuBindings() => new(null)
        {
            new ButtonBinding("Menu.Start",  (int) MenuAction.Start),
            new ButtonBinding("Menu.Select", (int) MenuAction.Select),

            new ButtonBinding("Menu.Green",  (int) MenuAction.Green),
            new ButtonBinding("Menu.Red",    (int) MenuAction.Red),
            new ButtonBinding("Menu.Yellow", (int) MenuAction.Yellow),
            new ButtonBinding("Menu.Blue",   (int) MenuAction.Blue),
            new ButtonBinding("Menu.Orange", (int) MenuAction.Orange),

            new ButtonBinding("Menu.Up",    (int) MenuAction.Up),
            new ButtonBinding("Menu.Down",  (int) MenuAction.Down),
            new ButtonBinding("Menu.Left",  (int) MenuAction.Left),
            new ButtonBinding("Menu.Right", (int) MenuAction.Right),
        };

        public static BindingCollection CreateFiveFretGuitarBindings() => new(GameMode.FiveFretGuitar)
        {
            new ButtonBinding("FiveFret.Green",  (int) GuitarAction.GreenFret),
            new ButtonBinding("FiveFret.Red",    (int) GuitarAction.RedFret),
            new ButtonBinding("FiveFret.Yellow", (int) GuitarAction.YellowFret),
            new ButtonBinding("FiveFret.Blue",   (int) GuitarAction.BlueFret),
            new ButtonBinding("FiveFret.Orange", (int) GuitarAction.OrangeFret),

            new ButtonBinding("Guitar.StrumUp",   (int) GuitarAction.StrumUp),
            new ButtonBinding("Guitar.StrumDown", (int) GuitarAction.StrumDown),

            new IndividualButtonBinding("Guitar.StarPower", (int) GuitarAction.StarPower),

            new AxisBinding("Guitar.Whammy", (int) GuitarAction.Whammy),
        };

        public static BindingCollection CreateSixFretGuitarBindings() => new(GameMode.SixFretGuitar)
        {
            new ButtonBinding("SixFret.Black1", (int) GuitarAction.Black1Fret),
            new ButtonBinding("SixFret.Black2", (int) GuitarAction.Black2Fret),
            new ButtonBinding("SixFret.Black3", (int) GuitarAction.Black3Fret),
            new ButtonBinding("SixFret.White1", (int) GuitarAction.White1Fret),
            new ButtonBinding("SixFret.White2", (int) GuitarAction.White2Fret),
            new ButtonBinding("SixFret.White3", (int) GuitarAction.White3Fret),

            new ButtonBinding("Guitar.StrumUp",   (int) GuitarAction.StrumUp),
            new ButtonBinding("Guitar.StrumDown", (int) GuitarAction.StrumDown),

            new IndividualButtonBinding("Guitar.StarPower", (int) GuitarAction.StarPower),

            new AxisBinding("Guitar.Whammy", (int) GuitarAction.Whammy),
        };

        public static BindingCollection CreateFourLaneDrumsBindings() => new(GameMode.FourLaneDrums)
        {
            new DrumPadButtonBinding("FourDrums.RedPad",    (int) DrumsAction.RedDrum),
            new DrumPadButtonBinding("FourDrums.YellowPad", (int) DrumsAction.YellowDrum),
            new DrumPadButtonBinding("FourDrums.BluePad",   (int) DrumsAction.BlueDrum),
            new DrumPadButtonBinding("FourDrums.GreenPad",  (int) DrumsAction.GreenDrum),

            new DrumPadButtonBinding("FourDrums.YellowCymbal", (int) DrumsAction.YellowCymbal),
            new DrumPadButtonBinding("FourDrums.BlueCymbal",   (int) DrumsAction.BlueCymbal),
            new DrumPadButtonBinding("FourDrums.GreenCymbal", "FourDrums.RedCymbal", (int) DrumsAction.GreenCymbal),

            new DrumPadButtonBinding("Drums.Kick", (int) DrumsAction.Kick),
        };

        public static BindingCollection CreateFiveLaneDrumsBindings() => new(GameMode.FiveLaneDrums)
        {
            new DrumPadButtonBinding("FiveDrums.RedPad",       (int) DrumsAction.RedDrum),
            new DrumPadButtonBinding("FiveDrums.YellowCymbal", (int) DrumsAction.YellowCymbal),
            new DrumPadButtonBinding("FiveDrums.BluePad",      (int) DrumsAction.BlueDrum),
            new DrumPadButtonBinding("FiveDrums.OrangeCymbal", (int) DrumsAction.OrangeCymbal),
            new DrumPadButtonBinding("FiveDrums.GreenPad",     (int) DrumsAction.GreenDrum),

            new DrumPadButtonBinding("Drums.Kick", (int) DrumsAction.Kick),
        };

        public static BindingCollection CreateProGuitarBindings() => new(GameMode.ProGuitar)
        {
            new IntegerBinding("ProGuitar.String1_Fret", (int) ProGuitarAction.String1_Fret),
            new IntegerBinding("ProGuitar.String2_Fret", (int) ProGuitarAction.String2_Fret),
            new IntegerBinding("ProGuitar.String3_Fret", (int) ProGuitarAction.String3_Fret),
            new IntegerBinding("ProGuitar.String4_Fret", (int) ProGuitarAction.String4_Fret),
            new IntegerBinding("ProGuitar.String5_Fret", (int) ProGuitarAction.String5_Fret),
            new IntegerBinding("ProGuitar.String6_Fret", (int) ProGuitarAction.String6_Fret),

            new ButtonBinding("ProGuitar.String1_Strum", (int) ProGuitarAction.String1_Strum),
            new ButtonBinding("ProGuitar.String2_Strum", (int) ProGuitarAction.String2_Strum),
            new ButtonBinding("ProGuitar.String3_Strum", (int) ProGuitarAction.String3_Strum),
            new ButtonBinding("ProGuitar.String4_Strum", (int) ProGuitarAction.String4_Strum),
            new ButtonBinding("ProGuitar.String5_Strum", (int) ProGuitarAction.String5_Strum),
            new ButtonBinding("ProGuitar.String6_Strum", (int) ProGuitarAction.String6_Strum),

            new IndividualButtonBinding("Guitar.StarPower", (int) ProGuitarAction.StarPower),
        };

        public static BindingCollection CreateProKeysBindings() => new(GameMode.ProKeys)
        {
            new ButtonBinding("ProKeys.Key1",  (int) ProKeysAction.Key1),
            new ButtonBinding("ProKeys.Key2",  (int) ProKeysAction.Key2),
            new ButtonBinding("ProKeys.Key3",  (int) ProKeysAction.Key3),
            new ButtonBinding("ProKeys.Key4",  (int) ProKeysAction.Key4),
            new ButtonBinding("ProKeys.Key5",  (int) ProKeysAction.Key5),

            new ButtonBinding("ProKeys.Key6",  (int) ProKeysAction.Key6),
            new ButtonBinding("ProKeys.Key7",  (int) ProKeysAction.Key7),
            new ButtonBinding("ProKeys.Key8",  (int) ProKeysAction.Key8),
            new ButtonBinding("ProKeys.Key9",  (int) ProKeysAction.Key9),
            new ButtonBinding("ProKeys.Key10", (int) ProKeysAction.Key10),
            new ButtonBinding("ProKeys.Key11", (int) ProKeysAction.Key11),
            new ButtonBinding("ProKeys.Key12", (int) ProKeysAction.Key12),

            new ButtonBinding("ProKeys.Key13", (int) ProKeysAction.Key13),
            new ButtonBinding("ProKeys.Key14", (int) ProKeysAction.Key14),
            new ButtonBinding("ProKeys.Key15", (int) ProKeysAction.Key15),
            new ButtonBinding("ProKeys.Key16", (int) ProKeysAction.Key16),
            new ButtonBinding("ProKeys.Key17", (int) ProKeysAction.Key17),

            new ButtonBinding("ProKeys.Key18", (int) ProKeysAction.Key18),
            new ButtonBinding("ProKeys.Key19", (int) ProKeysAction.Key19),
            new ButtonBinding("ProKeys.Key20", (int) ProKeysAction.Key20),
            new ButtonBinding("ProKeys.Key21", (int) ProKeysAction.Key21),
            new ButtonBinding("ProKeys.Key22", (int) ProKeysAction.Key22),
            new ButtonBinding("ProKeys.Key23", (int) ProKeysAction.Key23),
            new ButtonBinding("ProKeys.Key24", (int) ProKeysAction.Key24),

            new ButtonBinding("ProKeys.Key25", (int) ProKeysAction.Key25),

            new IndividualButtonBinding("ProKeys.StarPower", (int) ProKeysAction.StarPower),

            new AxisBinding("ProKeys.TouchEffects", (int) ProKeysAction.TouchEffects),
        };

        public static BindingCollection CreateVocalsBindings() => new(GameMode.Vocals)
        {
            // Only needed if we want to over-do it and run audio devices
            // through the Unity input system (which actually wouldn't be that hard lol)
            // new AxisBinding("Vocals.Pitch", (int) VocalsAction.Pitch),

            new IndividualButtonBinding("Vocals.Hit", (int) VocalsAction.Hit),
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
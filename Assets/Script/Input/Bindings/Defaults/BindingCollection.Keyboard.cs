using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Input;

namespace YARG.Input
{
    public partial class BindingCollection
    {
        private bool SetDefaultGameplayBindings(Keyboard keyboard)
        {
            return Mode switch
            {
                GameMode.FiveFretGuitar => SetDefaultFiveFretBindings(keyboard),
                GameMode.SixFretGuitar => SetDefaultSixFretBindings(keyboard),

                GameMode.FourLaneDrums => SetDefaultFourLaneBindings(keyboard),
                GameMode.FiveLaneDrums => SetDefaultFiveLaneBindings(keyboard),

                GameMode.ProGuitar => SetDefaultProGuitarBindings(keyboard),
                GameMode.ProKeys => SetDefaultProKeysBindings(keyboard),

                GameMode.Vocals => SetDefaultVocalsBindings(keyboard),

                _ => false
            };
        }

        private bool SetDefaultFiveFretBindings(Keyboard keyboard)
        {
            if (Mode != GameMode.FiveFretGuitar)
                return false;

            AddBinding(GuitarAction.GreenFret, keyboard.digit1Key);
            AddBinding(GuitarAction.RedFret, keyboard.digit2Key);
            AddBinding(GuitarAction.YellowFret, keyboard.digit3Key);
            AddBinding(GuitarAction.BlueFret, keyboard.digit4Key);
            AddBinding(GuitarAction.OrangeFret, keyboard.digit5Key);

            AddBinding(GuitarAction.StrumUp, keyboard.upArrowKey);
            AddBinding(GuitarAction.StrumDown, keyboard.downArrowKey);

            AddBinding(GuitarAction.StarPower, keyboard.backspaceKey);

            AddBinding(GuitarAction.Whammy, keyboard.semicolonKey);

            return true;
        }

        private bool SetDefaultSixFretBindings(Keyboard keyboard)
        {
            if (Mode != GameMode.SixFretGuitar)
                return false;

            AddBinding(GuitarAction.Black1Fret, keyboard.digit1Key);
            AddBinding(GuitarAction.Black2Fret, keyboard.digit2Key);
            AddBinding(GuitarAction.Black3Fret, keyboard.digit3Key);
            AddBinding(GuitarAction.White1Fret, keyboard.qKey);
            AddBinding(GuitarAction.White2Fret, keyboard.wKey);
            AddBinding(GuitarAction.White3Fret, keyboard.eKey);

            AddBinding(GuitarAction.StrumUp, keyboard.upArrowKey);
            AddBinding(GuitarAction.StrumDown, keyboard.downArrowKey);

            AddBinding(GuitarAction.StarPower, keyboard.backspaceKey);

            AddBinding(GuitarAction.Whammy, keyboard.semicolonKey);

            return true;
        }

        private bool SetDefaultFourLaneBindings(Keyboard keyboard)
        {
            if (Mode != GameMode.FourLaneDrums)
                return false;

            AddBinding(DrumsAction.RedDrum, keyboard.zKey);
            AddBinding(DrumsAction.YellowDrum, keyboard.xKey);
            AddBinding(DrumsAction.BlueDrum, keyboard.cKey);
            AddBinding(DrumsAction.GreenDrum, keyboard.vKey);

            AddBinding(DrumsAction.YellowCymbal, keyboard.sKey);
            AddBinding(DrumsAction.BlueCymbal, keyboard.dKey);
            AddBinding(DrumsAction.GreenCymbal, keyboard.fKey);

            AddBinding(DrumsAction.Kick, keyboard.spaceKey);

            return true;
        }

        private bool SetDefaultFiveLaneBindings(Keyboard keyboard)
        {
            if (Mode != GameMode.FiveLaneDrums)
                return false;

            AddBinding(DrumsAction.RedDrum, keyboard.zKey);
            AddBinding(DrumsAction.BlueDrum, keyboard.cKey);
            AddBinding(DrumsAction.GreenDrum, keyboard.bKey);

            AddBinding(DrumsAction.YellowCymbal, keyboard.xKey);
            AddBinding(DrumsAction.OrangeCymbal, keyboard.vKey);

            AddBinding(DrumsAction.Kick, keyboard.spaceKey);

            return true;
        }

        private bool SetDefaultProGuitarBindings(Keyboard keyboard)
        {
            if (Mode != GameMode.ProGuitar)
                return false;

            // Even on keyboard, this is nigh-impossible to bind lol
            return true;
        }

        private bool SetDefaultProKeysBindings(Keyboard keyboard)
        {
            if (Mode != GameMode.ProKeys)
                return false;

            // screw it, we ballin'

            // // Lower keyboard
            AddBinding(ProKeysAction.Key1, keyboard.zKey);
            AddBinding(ProKeysAction.Key2, keyboard.sKey);
            AddBinding(ProKeysAction.Key3, keyboard.xKey);
            AddBinding(ProKeysAction.Key4, keyboard.dKey);
            AddBinding(ProKeysAction.Key5, keyboard.cKey);

            AddBinding(ProKeysAction.Key6, keyboard.vKey);
            AddBinding(ProKeysAction.Key7, keyboard.gKey);
            AddBinding(ProKeysAction.Key8, keyboard.bKey);
            AddBinding(ProKeysAction.Key9, keyboard.hKey);
            AddBinding(ProKeysAction.Key10, keyboard.nKey);
            AddBinding(ProKeysAction.Key11, keyboard.jKey);
            AddBinding(ProKeysAction.Key12, keyboard.mKey);

            AddBinding(ProKeysAction.Key13, keyboard.commaKey);
            AddBinding(ProKeysAction.Key14, keyboard.lKey);
            AddBinding(ProKeysAction.Key15, keyboard.periodKey);
            AddBinding(ProKeysAction.Key16, keyboard.semicolonKey);
            AddBinding(ProKeysAction.Key17, keyboard.slashKey);

            // Higher keyboard
            AddBinding(ProKeysAction.Key6, keyboard.qKey);
            AddBinding(ProKeysAction.Key7, keyboard.digit2Key);
            AddBinding(ProKeysAction.Key8, keyboard.wKey);
            AddBinding(ProKeysAction.Key9, keyboard.digit3Key);
            AddBinding(ProKeysAction.Key10, keyboard.eKey);
            AddBinding(ProKeysAction.Key11, keyboard.digit4Key);
            AddBinding(ProKeysAction.Key12, keyboard.rKey);

            AddBinding(ProKeysAction.Key13, keyboard.tKey);
            AddBinding(ProKeysAction.Key14, keyboard.digit6Key);
            AddBinding(ProKeysAction.Key15, keyboard.yKey);
            AddBinding(ProKeysAction.Key16, keyboard.digit7Key);
            AddBinding(ProKeysAction.Key17, keyboard.uKey);

            AddBinding(ProKeysAction.Key18, keyboard.iKey);
            AddBinding(ProKeysAction.Key19, keyboard.digit9Key);
            AddBinding(ProKeysAction.Key20, keyboard.oKey);
            AddBinding(ProKeysAction.Key21, keyboard.digit0Key);
            AddBinding(ProKeysAction.Key22, keyboard.pKey);
            AddBinding(ProKeysAction.Key23, keyboard.minusKey);
            AddBinding(ProKeysAction.Key24, keyboard.leftBracketKey);

            AddBinding(ProKeysAction.Key25, keyboard.rightBracketKey);

            AddBinding(ProKeysAction.StarPower, keyboard.backspaceKey);

            AddBinding(ProKeysAction.TouchEffects, keyboard.quoteKey);

            return true;
        }

        private bool SetDefaultVocalsBindings(Keyboard keyboard)
        {
            if (Mode != GameMode.Vocals)
                return false;

            AddBinding(VocalsAction.Hit, keyboard.backspaceKey);

            return true;
        }

        private bool SetDefaultMenuBindings(Keyboard keyboard)
        {
            if (!IsMenu)
                return false;

            AddBinding(MenuAction.Start, keyboard.enterKey);
            AddBinding(MenuAction.Select, keyboard.backspaceKey);

            AddBinding(MenuAction.Green, keyboard.digit1Key);
            AddBinding(MenuAction.Red, keyboard.digit2Key);
            AddBinding(MenuAction.Yellow, keyboard.digit3Key);
            AddBinding(MenuAction.Blue, keyboard.digit4Key);
            AddBinding(MenuAction.Orange, keyboard.digit5Key);

            AddBinding(MenuAction.Up, keyboard.upArrowKey);
            AddBinding(MenuAction.Down, keyboard.downArrowKey);
            AddBinding(MenuAction.Left, keyboard.leftArrowKey);
            AddBinding(MenuAction.Right, keyboard.rightArrowKey);

            return true;
        }
    }
}
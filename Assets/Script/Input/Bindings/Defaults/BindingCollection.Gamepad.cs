using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Input;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_WSA
using UnityEngine.InputSystem.Switch;
#endif

namespace YARG.Input
{
    public partial class BindingCollection
    {
        private bool SetDefaultGameplayBindings(Gamepad gamepad, GamepadBindingMode mode)
        {
            return mode switch
            {
                GamepadBindingMode.Gamepad => SetDefaultGameplayBindings_Gamepad(gamepad),

                GamepadBindingMode.CrkdGuitar_Mode1 => SetDefaultGameplayBindings_CrkdGuitar(gamepad),

                GamepadBindingMode.WiitarThing_Guitar => SetDefaultGameplayBindings_Guitar(gamepad),
                GamepadBindingMode.WiitarThing_Drums => SetDefaultGameplayBindings_WiitarThing_Drums(gamepad),

                GamepadBindingMode.RB4InstrumentMapper_Guitar => SetDefaultGameplayBindings_Guitar(gamepad),
                GamepadBindingMode.RB4InstrumentMapper_GHLGuitar => SetDefaultGameplayBindings_GHLGuitar(gamepad),
                GamepadBindingMode.RB4InstrumentMapper_Drums => SetDefaultGameplayBindings_RB4InstrumentMapper_Drums(gamepad),

                _ => false
            };
        }

        private bool SetDefaultMenuBindings(Gamepad gamepad, GamepadBindingMode mode)
        {
            if (!IsMenu)
                return false;

            return mode switch
            {
                GamepadBindingMode.Gamepad => SetDefaultMenuBindings_Gamepad(gamepad),

                GamepadBindingMode.CrkdGuitar_Mode1 => SetDefaultMenuBindings_CrkdGuitar(gamepad),

                GamepadBindingMode.WiitarThing_Guitar => SetDefaultMenuBindings_Guitar(gamepad),
                GamepadBindingMode.WiitarThing_Drums => SetDefaultMenuBindings_WiitarThing_Drums(gamepad),

                GamepadBindingMode.RB4InstrumentMapper_Guitar => SetDefaultMenuBindings_Guitar(gamepad),
                GamepadBindingMode.RB4InstrumentMapper_Drums => SetDefaultMenuBindings_RB4InstrumentMapper_Drums(gamepad),

                _ => false
            };
        }

        #region Gamepad gameplay
        private bool SetDefaultGameplayBindings_Gamepad(Gamepad gamepad)
        {
            return Mode switch
            {
                GameMode.FiveFretGuitar => SetDefaultFiveFretBindings_Gamepad(gamepad),
                GameMode.SixFretGuitar => SetDefaultSixFretBindings_Gamepad(gamepad),

                GameMode.FourLaneDrums => SetDefaultFourLaneBindings_Gamepad(gamepad),
                GameMode.FiveLaneDrums => SetDefaultFiveLaneBindings_Gamepad(gamepad),

                // These ain't happening lol
                // GameMode.ProGuitar => SetDefaultProGuitarBindings_Gamepad(gamepad),
                // GameMode.ProKeys => SetDefaultProKeysBindings_Gamepad(gamepad),

                GameMode.Vocals => SetDefaultVocalsBindings_Gamepad(gamepad),

                _ => false
            };
        }

        private bool SetDefaultFiveFretBindings_Gamepad(Gamepad gamepad)
        {
            if (Mode != GameMode.FiveFretGuitar)
                return false;

            AddBinding(GuitarAction.GreenFret, gamepad.leftTrigger);
            AddBinding(GuitarAction.RedFret, gamepad.leftShoulder);
            AddBinding(GuitarAction.YellowFret, gamepad.rightShoulder);
            AddBinding(GuitarAction.BlueFret, gamepad.rightTrigger);
            AddBinding(GuitarAction.OrangeFret, gamepad.buttonSouth);

            AddBinding(GuitarAction.StrumUp, gamepad.dpad.up);
            AddBinding(GuitarAction.StrumDown, gamepad.dpad.down);

            AddBinding(GuitarAction.StarPower, gamepad.selectButton);

            AddBinding(GuitarAction.Whammy, gamepad.leftStick.x);

            return true;
        }

        private bool SetDefaultSixFretBindings_Gamepad(Gamepad gamepad)
        {
            if (Mode != GameMode.SixFretGuitar)
                return false;

            // Not sure what to do here, for now just leave it up to the user
            return true;
        }

        private bool SetDefaultFourLaneBindings_Gamepad(Gamepad gamepad)
        {
            if (Mode != GameMode.FourLaneDrums)
                return false;

            // Not sure what to do here, for now just leave it up to the user
            return true;
        }

        private bool SetDefaultFiveLaneBindings_Gamepad(Gamepad gamepad)
        {
            if (Mode != GameMode.FiveLaneDrums)
                return false;

            // Not sure what to do here, for now just leave it up to the user
            return true;
        }

        private bool SetDefaultVocalsBindings_Gamepad(Gamepad gamepad)
        {
            if (Mode != GameMode.Vocals)
                return false;

            AddBinding(VocalsAction.Hit, gamepad.aButton);
            AddBinding(VocalsAction.StarPower, gamepad.selectButton);

            return true;
        }

        private bool SetDefaultMenuBindings_Gamepad(Gamepad gamepad)
        {
            AddBinding(MenuAction.Start, gamepad.startButton);
            AddBinding(MenuAction.Select, gamepad.selectButton);

#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_WSA
            if (gamepad is SwitchProControllerHID switchPad)
            {
                // Swap A<->B and X<->Y for Switch controllers
                AddBinding(MenuAction.Green, switchPad.buttonEast);
                AddBinding(MenuAction.Red, switchPad.buttonSouth);
                AddBinding(MenuAction.Yellow, switchPad.buttonWest);
                AddBinding(MenuAction.Blue, switchPad.buttonNorth);
                AddBinding(MenuAction.Orange, switchPad.leftShoulder);
            }
            else
#endif
            {
                AddBinding(MenuAction.Green, gamepad.buttonSouth);
                AddBinding(MenuAction.Red, gamepad.buttonEast);
                AddBinding(MenuAction.Yellow, gamepad.buttonNorth);
                AddBinding(MenuAction.Blue, gamepad.buttonWest);
                AddBinding(MenuAction.Orange, gamepad.leftShoulder);
            }

            AddBinding(MenuAction.Up, gamepad.leftStick.up);
            AddBinding(MenuAction.Down, gamepad.leftStick.down);
            AddBinding(MenuAction.Left, gamepad.leftStick.left);
            AddBinding(MenuAction.Right, gamepad.leftStick.right);

            AddBinding(MenuAction.Up, gamepad.dpad.up);
            AddBinding(MenuAction.Down, gamepad.dpad.down);
            AddBinding(MenuAction.Left, gamepad.dpad.left);
            AddBinding(MenuAction.Right, gamepad.dpad.right);

            return true;
        }
        #endregion

        private bool SetDefaultGameplayBindings_Guitar(Gamepad gamepad)
        {
            if (Mode != GameMode.FiveFretGuitar)
                return false;

            AddBinding(GuitarAction.GreenFret, gamepad.aButton);
            AddBinding(GuitarAction.RedFret, gamepad.bButton);
            AddBinding(GuitarAction.YellowFret, gamepad.yButton);
            AddBinding(GuitarAction.BlueFret, gamepad.xButton);
            AddBinding(GuitarAction.OrangeFret, gamepad.leftShoulder);

            AddBinding(GuitarAction.StrumUp, gamepad.dpad.up);
            AddBinding(GuitarAction.StrumDown, gamepad.dpad.down);

            AddBinding(GuitarAction.StarPower, gamepad.selectButton);
            AddBinding(GuitarAction.StarPower, gamepad.rightStick.y, _tiltSettings);

            AddBinding(GuitarAction.Whammy, gamepad.rightStick.x);

            return true;
        }

        private bool SetDefaultGameplayBindings_CrkdGuitar(Gamepad gamepad)
        {
            if (Mode != GameMode.FiveFretGuitar)
                return false;

            AddBinding(GuitarAction.GreenFret, gamepad.aButton);
            AddBinding(GuitarAction.RedFret, gamepad.bButton);
            AddBinding(GuitarAction.YellowFret, gamepad.yButton);
            AddBinding(GuitarAction.BlueFret, gamepad.xButton);
            AddBinding(GuitarAction.OrangeFret, gamepad.leftShoulder);

            AddBinding(GuitarAction.StrumUp, gamepad.dpad.up);
            AddBinding(GuitarAction.StrumDown, gamepad.dpad.down);

            AddBinding(GuitarAction.StarPower, gamepad.selectButton);
            // CRKD mode 1 doesn't have a dedicated tilt axis
            // AddBinding(GuitarAction.StarPower, gamepad.rightStick.y, _tiltSettings);

            AddBinding(GuitarAction.Whammy, gamepad.leftTrigger);

            return true;
        }

        private bool SetDefaultGameplayBindings_GHLGuitar(Gamepad gamepad)
        {
            if (Mode != GameMode.FiveFretGuitar)
                return false;

            AddBinding(GuitarAction.Black1Fret, gamepad.aButton);
            AddBinding(GuitarAction.Black2Fret, gamepad.bButton);
            AddBinding(GuitarAction.Black3Fret, gamepad.yButton);
            AddBinding(GuitarAction.White1Fret, gamepad.xButton);
            AddBinding(GuitarAction.White2Fret, gamepad.leftShoulder);
            AddBinding(GuitarAction.White3Fret, gamepad.rightShoulder);

            AddBinding(GuitarAction.StrumUp, gamepad.dpad.up);
            AddBinding(GuitarAction.StrumDown, gamepad.dpad.down);

            AddBinding(GuitarAction.StarPower, gamepad.selectButton);
            AddBinding(GuitarAction.StarPower, gamepad.rightStick.y, _tiltSettings);

            AddBinding(GuitarAction.Whammy, gamepad.rightStick.x);

            return true;
        }

        private bool SetDefaultMenuBindings_Guitar(Gamepad gamepad)
        {
            AddBinding(MenuAction.Start, gamepad.startButton);
            AddBinding(MenuAction.Select, gamepad.selectButton);

            AddBinding(MenuAction.Green, gamepad.aButton);
            AddBinding(MenuAction.Red, gamepad.bButton);
            AddBinding(MenuAction.Yellow, gamepad.yButton);
            AddBinding(MenuAction.Blue, gamepad.xButton);
            AddBinding(MenuAction.Orange, gamepad.leftShoulder);

            AddBinding(MenuAction.Up, gamepad.dpad.up);
            AddBinding(MenuAction.Down, gamepad.dpad.down);
            AddBinding(MenuAction.Left, gamepad.dpad.left);
            AddBinding(MenuAction.Right, gamepad.dpad.right);

            return true;
        }

        private bool SetDefaultMenuBindings_CrkdGuitar(Gamepad gamepad)
        {
            AddBinding(MenuAction.Start, gamepad.startButton);
            AddBinding(MenuAction.Select, gamepad.selectButton);

            AddBinding(MenuAction.Green, gamepad.aButton);
            AddBinding(MenuAction.Red, gamepad.bButton);
            AddBinding(MenuAction.Yellow, gamepad.yButton);
            AddBinding(MenuAction.Blue, gamepad.xButton);
            AddBinding(MenuAction.Orange, gamepad.leftShoulder);

            AddBinding(MenuAction.Up, gamepad.dpad.up);
            AddBinding(MenuAction.Down, gamepad.dpad.down);
            AddBinding(MenuAction.Left, gamepad.dpad.left);
            AddBinding(MenuAction.Right, gamepad.dpad.right);

            AddBinding(MenuAction.Up, gamepad.leftStick.up);
            AddBinding(MenuAction.Down, gamepad.leftStick.down);
            AddBinding(MenuAction.Left, gamepad.leftStick.left);
            AddBinding(MenuAction.Right, gamepad.leftStick.right);

            return true;
        }

        #region WiitarThing
        private bool SetDefaultGameplayBindings_WiitarThing_Drums(Gamepad gamepad)
        {
            if (Mode != GameMode.FiveLaneDrums)
                return false;

            AddBinding(DrumsAction.RedDrum, gamepad.bButton);
            AddBinding(DrumsAction.BlueDrum, gamepad.xButton);
            AddBinding(DrumsAction.GreenDrum, gamepad.aButton);

            AddBinding(DrumsAction.YellowCymbal, gamepad.yButton);
            AddBinding(DrumsAction.OrangeCymbal, gamepad.rightShoulder);

            AddBinding(DrumsAction.Kick, gamepad.leftShoulder);

            return true;
        }

        private bool SetDefaultMenuBindings_WiitarThing_Drums(Gamepad gamepad)
        {
            if (!IsMenu)
                return false;

            AddBinding(MenuAction.Start, gamepad.startButton);
            AddBinding(MenuAction.Select, gamepad.selectButton);

            AddBinding(MenuAction.Green, gamepad.aButton);
            AddBinding(MenuAction.Red, gamepad.bButton);
            AddBinding(MenuAction.Blue, gamepad.xButton);

            AddBinding(MenuAction.Yellow, gamepad.yButton);
            AddBinding(MenuAction.Orange, gamepad.rightShoulder);

            AddBinding(MenuAction.Orange, gamepad.leftShoulder);

            AddBinding(MenuAction.Up, gamepad.dpad.up);
            AddBinding(MenuAction.Down, gamepad.dpad.down);
            AddBinding(MenuAction.Left, gamepad.dpad.left);
            AddBinding(MenuAction.Right, gamepad.dpad.right);

            return true;
        }
        #endregion

        #region RB4InstrumentMapper
        private bool SetDefaultGameplayBindings_RB4InstrumentMapper_Drums(Gamepad gamepad)
        {
            if (Mode != GameMode.FourLaneDrums)
                return false;

            AddBinding(DrumsAction.RedDrum, gamepad.bButton);
            AddBinding(DrumsAction.YellowDrum, gamepad.yButton);
            AddBinding(DrumsAction.BlueDrum, gamepad.xButton);
            AddBinding(DrumsAction.GreenDrum, gamepad.aButton);

            AddBinding(DrumsAction.YellowCymbal, gamepad.leftStickButton);
            AddBinding(DrumsAction.BlueCymbal, gamepad.rightStickButton);
            AddBinding(DrumsAction.GreenCymbal, gamepad.rightShoulder);

            AddBinding(DrumsAction.Kick, gamepad.leftShoulder);
            AddBinding(DrumsAction.Kick, gamepad.leftTrigger);

            return true;
        }

        private bool SetDefaultMenuBindings_RB4InstrumentMapper_Drums(Gamepad gamepad)
        {
            if (!IsMenu)
                return false;

            AddBinding(MenuAction.Start, gamepad.startButton);
            AddBinding(MenuAction.Select, gamepad.selectButton);

            AddBinding(MenuAction.Red, gamepad.bButton);
            AddBinding(MenuAction.Up, gamepad.yButton);
            AddBinding(MenuAction.Down, gamepad.xButton);
            AddBinding(MenuAction.Green, gamepad.aButton);

            AddBinding(MenuAction.Yellow, gamepad.leftStickButton);
            AddBinding(MenuAction.Blue, gamepad.rightStickButton);
            AddBinding(MenuAction.Green, gamepad.rightShoulder);

            AddBinding(MenuAction.Orange, gamepad.leftShoulder);

            AddBinding(MenuAction.Up, gamepad.dpad.up);
            AddBinding(MenuAction.Down, gamepad.dpad.down);
            AddBinding(MenuAction.Left, gamepad.dpad.left);
            AddBinding(MenuAction.Right, gamepad.dpad.right);

            return true;
        }
        #endregion
    }
}
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XInput;
using YARG.Core;
using YARG.Core.Input;
using YARG.Menu.Persistent;

namespace YARG.Input
{
    public partial class BindingCollection
    {
        // Cache for gamepads that have been prompted for this session
        private static readonly Dictionary<XInputController, GamepadMode> _xinputGamepads = new();

        private enum GamepadMode
        {
            Gamepad,
            WiitarThing_Guitar,
            WiitarThing_Drums,
            RB4InstrumentMapper_Guitar,
            RB4InstrumentMapper_GHLGuitar,
            RB4InstrumentMapper_Drums,
        }

        private bool SetDefaultGameplayBindings(Gamepad gamepad)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (gamepad is XInputController xinput)
            {
                _SetDefaultGameplayBindings_Windows(xinput).Forget();
                return true;
            }
#endif
            return SetDefaultGameplayBindings_Gamepad(gamepad);
        }

        private bool SetDefaultMenuBindings(Gamepad gamepad)
        {
            if (!IsMenu)
                return false;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (gamepad is XInputController xinput)
            {
                _SetDefaultMenuBindings_Windows(xinput).Forget();
                return true;
            }
#endif
            return SetDefaultMenuBindings_Gamepad(gamepad);
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

            AddBinding(VocalsAction.Hit, gamepad.selectButton);

            return true;
        }

        private bool SetDefaultMenuBindings_Gamepad(Gamepad gamepad)
        {
            AddBinding(MenuAction.Start, gamepad.startButton);
            AddBinding(MenuAction.Select, gamepad.selectButton);

            AddBinding(MenuAction.Green, gamepad.buttonSouth);
            AddBinding(MenuAction.Red, gamepad.buttonEast);
            AddBinding(MenuAction.Yellow, gamepad.buttonNorth);
            AddBinding(MenuAction.Blue, gamepad.buttonWest);
            AddBinding(MenuAction.Orange, gamepad.leftShoulder);

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

        private async UniTask<GamepadMode> PromptGamepadMode(XInputController xinput)
        {
            await DialogManager.Instance.WaitUntilCurrentClosed();

            // Check if this gamepad has been prompted for already
            if (_xinputGamepads.TryGetValue(xinput, out var mode))
                return mode;

            // Add to cache before prompting to prevent ordering issues
            mode = GamepadMode.Gamepad;
            _xinputGamepads.Add(xinput, mode);

            var dialog = DialogManager.Instance.ShowList("Which kind of controller is this?");
            dialog.AddListButton("Gamepad", () => mode = GamepadMode.Gamepad);
            dialog.AddListButton("WiitarThing Guitar", () => mode = GamepadMode.WiitarThing_Guitar);
            dialog.AddListButton("WiitarThing Drumkit", () => mode = GamepadMode.WiitarThing_Drums);
            dialog.AddListButton("RB4InstrumentMapper Guitar", () => mode = GamepadMode.RB4InstrumentMapper_Guitar);
            dialog.AddListButton("RB4InstrumentMapper GHL Guitar", () => mode = GamepadMode.RB4InstrumentMapper_GHLGuitar);
            dialog.AddListButton("RB4InstrumentMapper Drumkit", () => mode = GamepadMode.RB4InstrumentMapper_Drums);
            await dialog.WaitUntilClosed();

            // Cache so we only prompt once
            _xinputGamepads[xinput] = mode;

            return mode;
        }

        private async UniTask<bool> _SetDefaultGameplayBindings_Windows(XInputController xinput)
        {
            // Some remappers emulate gamepads, prompt user for what kind of device this is
            var mode = await PromptGamepadMode(xinput);

            // Skip if the gamepad is no longer present
            if (!xinput.added)
                return false;

            return mode switch
            {
                GamepadMode.Gamepad => SetDefaultGameplayBindings_Gamepad(xinput),

                GamepadMode.WiitarThing_Guitar => SetDefaultGameplayBindings_Guitar(xinput),
                GamepadMode.WiitarThing_Drums => SetDefaultGameplayBindings_WiitarThing_Drums(xinput),

                GamepadMode.RB4InstrumentMapper_Guitar => SetDefaultGameplayBindings_Guitar(xinput),
                GamepadMode.RB4InstrumentMapper_GHLGuitar => SetDefaultGameplayBindings_GHLGuitar(xinput),
                GamepadMode.RB4InstrumentMapper_Drums => SetDefaultGameplayBindings_RB4InstrumentMapper_Drums(xinput),

                _ => false
            };
        }

        private async UniTask<bool> _SetDefaultMenuBindings_Windows(XInputController xinput)
        {
            // Some remappers emulate gamepads, prompt user for what kind of device this is
            var mode = await PromptGamepadMode(xinput);

            // Skip if the gamepad is no longer present
            if (!xinput.added)
                return false;

            return mode switch
            {
                GamepadMode.Gamepad => SetDefaultMenuBindings_Gamepad(xinput),

                GamepadMode.WiitarThing_Guitar => SetDefaultMenuBindings_Guitar(xinput),
                GamepadMode.WiitarThing_Drums => SetDefaultMenuBindings_WiitarThing_Drums(xinput),

                GamepadMode.RB4InstrumentMapper_Guitar => SetDefaultMenuBindings_Guitar(xinput),
                GamepadMode.RB4InstrumentMapper_Drums => SetDefaultMenuBindings_RB4InstrumentMapper_Drums(xinput),

                _ => false
            };
        }

        private bool SetDefaultGameplayBindings_Guitar(XInputController xinput)
        {
            if (Mode != GameMode.FiveFretGuitar)
                return false;

            AddBinding(GuitarAction.GreenFret, xinput.aButton);
            AddBinding(GuitarAction.RedFret, xinput.bButton);
            AddBinding(GuitarAction.YellowFret, xinput.yButton);
            AddBinding(GuitarAction.BlueFret, xinput.xButton);
            AddBinding(GuitarAction.OrangeFret, xinput.leftShoulder);

            AddBinding(GuitarAction.StrumUp, xinput.dpad.up);
            AddBinding(GuitarAction.StrumDown, xinput.dpad.down);

            AddBinding(GuitarAction.StarPower, xinput.selectButton);
            AddBinding(GuitarAction.StarPower, xinput.rightStick.y, _tiltSettings);

            AddBinding(GuitarAction.Whammy, xinput.rightStick.x);

            return true;
        }

        private bool SetDefaultGameplayBindings_GHLGuitar(XInputController xinput)
        {
            if (Mode != GameMode.FiveFretGuitar)
                return false;

            AddBinding(GuitarAction.Black1Fret, xinput.aButton);
            AddBinding(GuitarAction.Black2Fret, xinput.bButton);
            AddBinding(GuitarAction.Black3Fret, xinput.yButton);
            AddBinding(GuitarAction.White1Fret, xinput.xButton);
            AddBinding(GuitarAction.White2Fret, xinput.leftShoulder);
            AddBinding(GuitarAction.White3Fret, xinput.rightShoulder);

            AddBinding(GuitarAction.StrumUp, xinput.dpad.up);
            AddBinding(GuitarAction.StrumDown, xinput.dpad.down);

            AddBinding(GuitarAction.StarPower, xinput.selectButton);
            AddBinding(GuitarAction.StarPower, xinput.rightStick.y, _tiltSettings);

            AddBinding(GuitarAction.Whammy, xinput.rightStick.x);

            return true;
        }

        private bool SetDefaultMenuBindings_Guitar(XInputController xinput)
        {
            AddBinding(MenuAction.Start, xinput.startButton);
            AddBinding(MenuAction.Select, xinput.selectButton);

            AddBinding(MenuAction.Green, xinput.aButton);
            AddBinding(MenuAction.Red, xinput.bButton);
            AddBinding(MenuAction.Yellow, xinput.yButton);
            AddBinding(MenuAction.Blue, xinput.xButton);
            AddBinding(MenuAction.Orange, xinput.leftShoulder);

            AddBinding(MenuAction.Up, xinput.dpad.up);
            AddBinding(MenuAction.Down, xinput.dpad.down);
            AddBinding(MenuAction.Left, xinput.dpad.left);
            AddBinding(MenuAction.Right, xinput.dpad.right);

            return true;
        }

        #region WiitarThing
        private bool SetDefaultGameplayBindings_WiitarThing_Drums(XInputController xinput)
        {
            if (Mode != GameMode.FiveLaneDrums)
                return false;

            AddBinding(DrumsAction.RedDrum, xinput.bButton);
            AddBinding(DrumsAction.BlueDrum, xinput.xButton);
            AddBinding(DrumsAction.GreenDrum, xinput.aButton);

            AddBinding(DrumsAction.YellowCymbal, xinput.yButton);
            AddBinding(DrumsAction.OrangeCymbal, xinput.rightShoulder);

            AddBinding(DrumsAction.Kick, xinput.leftShoulder);

            return true;
        }

        private bool SetDefaultMenuBindings_WiitarThing_Drums(XInputController xinput)
        {
            if (!IsMenu)
                return false;

            AddBinding(MenuAction.Start, xinput.startButton);
            AddBinding(MenuAction.Select, xinput.selectButton);

            AddBinding(MenuAction.Green, xinput.aButton);
            AddBinding(MenuAction.Red, xinput.bButton);
            AddBinding(MenuAction.Blue, xinput.xButton);

            AddBinding(MenuAction.Yellow, xinput.yButton);
            AddBinding(MenuAction.Orange, xinput.rightShoulder);

            AddBinding(MenuAction.Orange, xinput.leftShoulder);

            AddBinding(MenuAction.Up, xinput.dpad.up);
            AddBinding(MenuAction.Down, xinput.dpad.down);
            AddBinding(MenuAction.Left, xinput.dpad.left);
            AddBinding(MenuAction.Right, xinput.dpad.right);

            return true;
        }
        #endregion

        #region RB4InstrumentMapper
        private bool SetDefaultGameplayBindings_RB4InstrumentMapper_Drums(XInputController xinput)
        {
            if (Mode != GameMode.FourLaneDrums)
                return false;

            AddBinding(DrumsAction.RedDrum, xinput.bButton);
            AddBinding(DrumsAction.YellowDrum, xinput.yButton);
            AddBinding(DrumsAction.BlueDrum, xinput.xButton);
            AddBinding(DrumsAction.GreenDrum, xinput.aButton);

            AddBinding(DrumsAction.YellowCymbal, xinput.leftStickButton);
            AddBinding(DrumsAction.BlueCymbal, xinput.rightStickButton);
            AddBinding(DrumsAction.GreenCymbal, xinput.rightShoulder);

            AddBinding(DrumsAction.Kick, xinput.leftShoulder);
            AddBinding(DrumsAction.Kick, xinput.leftTrigger);

            return true;
        }

        private bool SetDefaultMenuBindings_RB4InstrumentMapper_Drums(XInputController xinput)
        {
            if (!IsMenu)
                return false;

            AddBinding(MenuAction.Start, xinput.startButton);
            AddBinding(MenuAction.Select, xinput.selectButton);

            AddBinding(MenuAction.Red, xinput.bButton);
            AddBinding(MenuAction.Up, xinput.yButton);
            AddBinding(MenuAction.Down, xinput.xButton);
            AddBinding(MenuAction.Green, xinput.aButton);

            AddBinding(MenuAction.Yellow, xinput.leftStickButton);
            AddBinding(MenuAction.Blue, xinput.rightStickButton);
            AddBinding(MenuAction.Green, xinput.rightShoulder);

            AddBinding(MenuAction.Orange, xinput.leftShoulder);

            AddBinding(MenuAction.Up, xinput.dpad.up);
            AddBinding(MenuAction.Down, xinput.dpad.down);
            AddBinding(MenuAction.Left, xinput.dpad.left);
            AddBinding(MenuAction.Right, xinput.dpad.right);

            return true;
        }
        #endregion
    }
}
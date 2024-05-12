using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Input;

namespace YARG.Input
{
    public partial class BindingCollection
    {
        private bool SetDefaultGameplayBindings(Gamepad gamepad)
        {
            return Mode switch
            {
                GameMode.FiveFretGuitar => SetDefaultFiveFretBindings(gamepad),
                GameMode.SixFretGuitar => SetDefaultSixFretBindings(gamepad),

                GameMode.FourLaneDrums => SetDefaultFourLaneBindings(gamepad),
                GameMode.FiveLaneDrums => SetDefaultFiveLaneBindings(gamepad),

                GameMode.ProGuitar => SetDefaultProGuitarBindings(gamepad),
                GameMode.ProKeys => SetDefaultProKeysBindings(gamepad),

                GameMode.Vocals => SetDefaultVocalsBindings(gamepad),

                _ => false
            };
        }

        private bool SetDefaultFiveFretBindings(Gamepad gamepad)
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

        private bool SetDefaultSixFretBindings(Gamepad gamepad)
        {
            if (Mode != GameMode.SixFretGuitar)
                return false;

            // Not sure what to do here, for now just leave it up to the user
            return true;
        }

        private bool SetDefaultFourLaneBindings(Gamepad gamepad)
        {
            if (Mode != GameMode.FourLaneDrums)
                return false;

            // Not sure what to do here, for now just leave it up to the user
            return true;
        }

        private bool SetDefaultFiveLaneBindings(Gamepad gamepad)
        {
            if (Mode != GameMode.FiveLaneDrums)
                return false;

            // Not sure what to do here, for now just leave it up to the user
            return true;
        }

        private bool SetDefaultProGuitarBindings(Gamepad gamepad)
        {
            if (Mode != GameMode.ProGuitar)
                return false;

            // No way this is happening lol
            return true;
        }

        private bool SetDefaultProKeysBindings(Gamepad gamepad)
        {
            if (Mode != GameMode.ProKeys)
                return false;

            // This one ain't happening either
            return true;
        }

        private bool SetDefaultVocalsBindings(Gamepad gamepad)
        {
            if (Mode != GameMode.Vocals)
                return false;

            AddBinding(VocalsAction.StarPower, gamepad.selectButton);

            return true;
        }

        private bool SetDefaultMenuBindings(Gamepad gamepad)
        {
            if (!IsMenu)
                return false;

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
    }
}
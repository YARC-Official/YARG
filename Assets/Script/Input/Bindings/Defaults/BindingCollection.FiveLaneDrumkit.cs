using PlasticBand.Devices;
using YARG.Core;
using YARG.Core.Input;

namespace YARG.Input
{
    public partial class BindingCollection
    {
        private bool SetDefaultGameplayBindings(FiveLaneDrumkit drums)
        {
            if (Mode != GameMode.FiveLaneDrums)
                return false;

            AddBinding(DrumsAction.RedDrum, drums.redPad);
            AddBinding(DrumsAction.BlueDrum, drums.bluePad);
            AddBinding(DrumsAction.GreenDrum, drums.greenPad);

            AddBinding(DrumsAction.YellowCymbal, drums.yellowCymbal);
            AddBinding(DrumsAction.OrangeCymbal, drums.orangeCymbal);

            AddBinding(DrumsAction.Kick, drums.kick);

            return true;
        }

        private bool SetDefaultMenuBindings(FiveLaneDrumkit drums)
        {
            if (!IsMenu)
                return false;

            AddBinding(MenuAction.Start, drums.startButton);
            AddBinding(MenuAction.Select, drums.selectButton);

            AddBinding(MenuAction.Green, drums.buttonSouth); // A, cross
            AddBinding(MenuAction.Red, drums.buttonEast); // B, circle
            AddBinding(MenuAction.Blue, drums.buttonWest); // X, square
            AddBinding(MenuAction.Yellow, drums.buttonNorth); // Y, triangle

            AddBinding(MenuAction.Green, drums.greenPad);
            AddBinding(MenuAction.Red, drums.redPad);
            AddBinding(MenuAction.Blue, drums.bluePad);

            AddBinding(MenuAction.Yellow, drums.yellowCymbal);
            AddBinding(MenuAction.Orange, drums.orangeCymbal);

            AddBinding(MenuAction.Orange, drums.kick);

            AddBinding(MenuAction.Up, drums.dpad.up);
            AddBinding(MenuAction.Down, drums.dpad.down);
            AddBinding(MenuAction.Left, drums.dpad.left);
            AddBinding(MenuAction.Right, drums.dpad.right);

            return true;
        }
    }
}
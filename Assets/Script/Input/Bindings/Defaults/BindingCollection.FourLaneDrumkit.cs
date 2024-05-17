using PlasticBand.Devices;
using YARG.Core;
using YARG.Core.Input;

namespace YARG.Input
{
    public partial class BindingCollection
    {
        private bool SetDefaultGameplayBindings(FourLaneDrumkit drums)
        {
            if (Mode != GameMode.FourLaneDrums)
                return false;

            AddBinding(DrumsAction.RedDrum, drums.redPad);
            AddBinding(DrumsAction.YellowDrum, drums.yellowPad);
            AddBinding(DrumsAction.BlueDrum, drums.bluePad);
            AddBinding(DrumsAction.GreenDrum, drums.greenPad);

            AddBinding(DrumsAction.YellowCymbal, drums.yellowCymbal);
            AddBinding(DrumsAction.BlueCymbal, drums.blueCymbal);
            AddBinding(DrumsAction.GreenCymbal, drums.greenCymbal);

            AddBinding(DrumsAction.Kick, drums.kick1);
            AddBinding(DrumsAction.Kick, drums.kick2);

            return true;
        }

        private bool SetDefaultMenuBindings(FourLaneDrumkit drums)
        {
            if (!IsMenu)
                return false;

            AddBinding(MenuAction.Start, drums.startButton);
            AddBinding(MenuAction.Select, drums.selectButton);

            AddBinding(MenuAction.Green, drums.buttonSouth); // A, cross
            AddBinding(MenuAction.Red, drums.buttonEast); // B, circle
            AddBinding(MenuAction.Blue, drums.buttonWest); // X, square, 1
            AddBinding(MenuAction.Yellow, drums.buttonNorth); // Y, triangle, 2

            AddBinding(MenuAction.Red, drums.redPad);
            AddBinding(MenuAction.Up, drums.yellowPad);
            AddBinding(MenuAction.Down, drums.bluePad);
            AddBinding(MenuAction.Green, drums.greenPad);

            AddBinding(MenuAction.Yellow, drums.yellowCymbal);
            AddBinding(MenuAction.Blue, drums.blueCymbal);
            AddBinding(MenuAction.Green, drums.greenCymbal);

            AddBinding(MenuAction.Orange, drums.kick1);

            AddBinding(MenuAction.Up, drums.dpad.up);
            AddBinding(MenuAction.Down, drums.dpad.down);
            AddBinding(MenuAction.Left, drums.dpad.left);
            AddBinding(MenuAction.Right, drums.dpad.right);

            return true;
        }
    }
}
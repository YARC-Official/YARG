using PlasticBand.Devices;
using YARG.Core;
using YARG.Core.Input;

namespace YARG.Input
{
    public partial class BindingCollection
    {
        private bool SetDefaultGameplayBindings(SixFretGuitar guitar)
        {
            if (Mode != GameMode.SixFretGuitar)
                return false;

            AddBinding(GuitarAction.Black1Fret, guitar.black1);
            AddBinding(GuitarAction.Black2Fret, guitar.black2);
            AddBinding(GuitarAction.Black3Fret, guitar.black3);
            AddBinding(GuitarAction.White1Fret, guitar.white1);
            AddBinding(GuitarAction.White2Fret, guitar.white2);
            AddBinding(GuitarAction.White3Fret, guitar.white3);

            AddBinding(GuitarAction.StrumUp, guitar.strumUp);
            AddBinding(GuitarAction.StrumDown, guitar.strumDown);

            AddBinding(GuitarAction.StarPower, guitar.selectButton);
            AddBinding(GuitarAction.StarPower, guitar.tilt, _tiltSettings);

            AddBinding(GuitarAction.Whammy, guitar.whammy);

            return true;
        }

        private bool SetDefaultMenuBindings(SixFretGuitar guitar)
        {
            if (!IsMenu)
                return false;

            AddBinding(MenuAction.Start, guitar.startButton);
            AddBinding(MenuAction.Select, guitar.selectButton);

            AddBinding(MenuAction.Green, guitar.black1);
            AddBinding(MenuAction.Red, guitar.black2);
            AddBinding(MenuAction.Yellow, guitar.black3);
            AddBinding(MenuAction.Blue, guitar.white1);
            AddBinding(MenuAction.Orange, guitar.white2);

            AddBinding(MenuAction.Up, guitar.dpad.up);
            AddBinding(MenuAction.Down, guitar.dpad.down);
            AddBinding(MenuAction.Left, guitar.dpad.left);
            AddBinding(MenuAction.Right, guitar.dpad.right);

            return true;
        }
    }
}
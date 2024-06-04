using PlasticBand.Devices;
using YARG.Core;
using YARG.Core.Input;

namespace YARG.Input
{
    public partial class BindingCollection
    {
        private static ActuationSettings _tiltSettings = new() { ButtonPressThreshold = 1f };

        private bool SetDefaultGameplayBindings(FiveFretGuitar guitar)
        {
            if (Mode != GameMode.FiveFretGuitar)
                return false;

            AddBinding(GuitarAction.GreenFret, guitar.greenFret);
            AddBinding(GuitarAction.RedFret, guitar.redFret);
            AddBinding(GuitarAction.YellowFret, guitar.yellowFret);
            AddBinding(GuitarAction.BlueFret, guitar.blueFret);
            AddBinding(GuitarAction.OrangeFret, guitar.orangeFret);

            AddBinding(GuitarAction.StrumUp, guitar.strumUp);
            AddBinding(GuitarAction.StrumDown, guitar.strumDown);

            AddBinding(GuitarAction.StarPower, guitar.selectButton);
            AddBinding(GuitarAction.StarPower, guitar.tilt, _tiltSettings);

            AddBinding(GuitarAction.Whammy, guitar.whammy);

            if (guitar is GuitarHeroGuitar gh)
            {
                AddBinding(GuitarAction.StarPower, gh.spPedal);
            }
            else if (guitar is RockBandGuitar rb)
            {
                AddBinding(GuitarAction.GreenFret, rb.soloGreen);
                AddBinding(GuitarAction.RedFret, rb.soloRed);
                AddBinding(GuitarAction.YellowFret, rb.soloYellow);
                AddBinding(GuitarAction.BlueFret, rb.soloBlue);
                AddBinding(GuitarAction.OrangeFret, rb.soloOrange);
            }

            return true;
        }

        private bool SetDefaultMenuBindings(FiveFretGuitar guitar)
        {
            if (!IsMenu)
                return false;

            AddBinding(MenuAction.Start, guitar.startButton);
            AddBinding(MenuAction.Select, guitar.selectButton);

            AddBinding(MenuAction.Green, guitar.greenFret);
            AddBinding(MenuAction.Red, guitar.redFret);
            AddBinding(MenuAction.Yellow, guitar.yellowFret);
            AddBinding(MenuAction.Blue, guitar.blueFret);
            AddBinding(MenuAction.Orange, guitar.orangeFret);

            AddBinding(MenuAction.Up, guitar.dpad.up);
            AddBinding(MenuAction.Down, guitar.dpad.down);
            AddBinding(MenuAction.Left, guitar.dpad.left);
            AddBinding(MenuAction.Right, guitar.dpad.right);

            if (guitar is RockBandGuitar rb)
            {
                AddBinding(GuitarAction.GreenFret, rb.soloGreen);
                AddBinding(GuitarAction.RedFret, rb.soloRed);
                AddBinding(GuitarAction.YellowFret, rb.soloYellow);
                AddBinding(GuitarAction.BlueFret, rb.soloBlue);
                AddBinding(GuitarAction.OrangeFret, rb.soloOrange);

                if (guitar is RiffmasterGuitar riff)
                {
                    AddBinding(MenuAction.Up, riff.joystick.up);
                    AddBinding(MenuAction.Down, riff.joystick.down);
                    AddBinding(MenuAction.Left, riff.joystick.left);
                    AddBinding(MenuAction.Right, riff.joystick.right);
                }
            }

            return true;
        }
    }
}
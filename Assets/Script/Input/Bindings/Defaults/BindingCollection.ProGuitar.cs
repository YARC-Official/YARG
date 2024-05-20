using PlasticBand.Devices;
using YARG.Core;
using YARG.Core.Input;

namespace YARG.Input
{
    public partial class BindingCollection
    {
        private bool SetDefaultGameplayBindings(ProGuitar guitar)
        {
            if (Mode != GameMode.ProGuitar)
                return false;

            switch (Mode)
            {
                case GameMode.FiveFretGuitar:
                    AddBinding(GuitarAction.GreenFret, guitar.greenFret);
                    AddBinding(GuitarAction.RedFret, guitar.redFret);
                    AddBinding(GuitarAction.YellowFret, guitar.yellowFret);
                    AddBinding(GuitarAction.BlueFret, guitar.blueFret);
                    AddBinding(GuitarAction.OrangeFret, guitar.orangeFret);

                    // TODO: This could probably be handled better
                    AddBinding(GuitarAction.StrumDown, guitar.strum1);

                    AddBinding(GuitarAction.StarPower, guitar.selectButton);
                    AddBinding(GuitarAction.StarPower, guitar.tilt, _tiltSettings);
                    AddBinding(GuitarAction.StarPower, guitar.digitalPedal);
                    break;

                case GameMode.ProGuitar:
                    AddBinding(ProGuitarAction.String1_Fret, guitar.fret1);
                    AddBinding(ProGuitarAction.String2_Fret, guitar.fret2);
                    AddBinding(ProGuitarAction.String3_Fret, guitar.fret3);
                    AddBinding(ProGuitarAction.String4_Fret, guitar.fret4);
                    AddBinding(ProGuitarAction.String5_Fret, guitar.fret5);
                    AddBinding(ProGuitarAction.String6_Fret, guitar.fret6);

                    AddBinding(ProGuitarAction.String1_Strum, guitar.strum1);
                    AddBinding(ProGuitarAction.String2_Strum, guitar.strum2);
                    AddBinding(ProGuitarAction.String3_Strum, guitar.strum3);
                    AddBinding(ProGuitarAction.String4_Strum, guitar.strum4);
                    AddBinding(ProGuitarAction.String5_Strum, guitar.strum5);
                    AddBinding(ProGuitarAction.String6_Strum, guitar.strum6);

                    AddBinding(ProGuitarAction.StarPower, guitar.selectButton);
                    AddBinding(ProGuitarAction.StarPower, guitar.tilt, _tiltSettings);
                    AddBinding(ProGuitarAction.StarPower, guitar.digitalPedal);
                    break;

                default:
                    return false;
            }

            return true;
        }

        private bool SetDefaultMenuBindings(ProGuitar guitar)
        {
            if (!IsMenu)
                return false;

            AddBinding(MenuAction.Start, guitar.startButton);
            AddBinding(MenuAction.Select, guitar.selectButton);

            AddBinding(MenuAction.Green, guitar.buttonSouth); // A, cross
            AddBinding(MenuAction.Red, guitar.buttonEast); // B, circle
            AddBinding(MenuAction.Blue, guitar.buttonWest); // X, square, 1
            AddBinding(MenuAction.Yellow, guitar.buttonNorth); // Y, triangle, 2

            AddBinding(MenuAction.Green, guitar.greenFret);
            AddBinding(MenuAction.Red, guitar.redFret);
            AddBinding(MenuAction.Yellow, guitar.yellowFret);
            AddBinding(MenuAction.Blue, guitar.blueFret);
            AddBinding(MenuAction.Orange, guitar.orangeFret);

            AddBinding(MenuAction.Up, guitar.dpad.up);
            AddBinding(MenuAction.Down, guitar.dpad.down);
            AddBinding(MenuAction.Left, guitar.dpad.left);
            AddBinding(MenuAction.Right, guitar.dpad.right);

            return true;
        }
    }
}
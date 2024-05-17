using PlasticBand.Devices;
using YARG.Core;
using YARG.Core.Input;

namespace YARG.Input
{
    public partial class BindingCollection
    {
        private bool SetDefaultGameplayBindings(ProKeyboard keyboard)
        {
            if (Mode != GameMode.ProKeys)
                return false;

            switch (Mode)
            {
                case GameMode.FiveFretGuitar:
                    AddBinding(GuitarAction.GreenFret, keyboard.key1);
                    AddBinding(GuitarAction.RedFret, keyboard.key3);
                    AddBinding(GuitarAction.YellowFret, keyboard.key5);
                    AddBinding(GuitarAction.BlueFret, keyboard.key6);
                    AddBinding(GuitarAction.OrangeFret, keyboard.key8);

                    AddBinding(GuitarAction.GreenFret, keyboard.key13);
                    AddBinding(GuitarAction.RedFret, keyboard.key15);
                    AddBinding(GuitarAction.YellowFret, keyboard.key17);
                    AddBinding(GuitarAction.BlueFret, keyboard.key18);
                    AddBinding(GuitarAction.OrangeFret, keyboard.key20);

                    AddBinding(GuitarAction.StarPower, keyboard.overdrive);
                    AddBinding(GuitarAction.StarPower, keyboard.selectButton);
                    AddBinding(GuitarAction.StarPower, keyboard.digitalPedal);

                    AddBinding(GuitarAction.Whammy, keyboard.touchStrip);
                    AddBinding(GuitarAction.Whammy, keyboard.analogPedal);
                    return true;

                case GameMode.ProKeys:
                    AddBinding(ProKeysAction.Key1, keyboard.key1);
                    AddBinding(ProKeysAction.Key2, keyboard.key2);
                    AddBinding(ProKeysAction.Key3, keyboard.key3);
                    AddBinding(ProKeysAction.Key4, keyboard.key4);
                    AddBinding(ProKeysAction.Key5, keyboard.key5);

                    AddBinding(ProKeysAction.Key6, keyboard.key6);
                    AddBinding(ProKeysAction.Key7, keyboard.key7);
                    AddBinding(ProKeysAction.Key8, keyboard.key8);
                    AddBinding(ProKeysAction.Key9, keyboard.key9);
                    AddBinding(ProKeysAction.Key10, keyboard.key10);
                    AddBinding(ProKeysAction.Key11, keyboard.key11);
                    AddBinding(ProKeysAction.Key12, keyboard.key12);

                    AddBinding(ProKeysAction.Key13, keyboard.key13);
                    AddBinding(ProKeysAction.Key14, keyboard.key14);
                    AddBinding(ProKeysAction.Key15, keyboard.key15);
                    AddBinding(ProKeysAction.Key16, keyboard.key16);
                    AddBinding(ProKeysAction.Key17, keyboard.key17);

                    AddBinding(ProKeysAction.Key18, keyboard.key18);
                    AddBinding(ProKeysAction.Key19, keyboard.key19);
                    AddBinding(ProKeysAction.Key20, keyboard.key20);
                    AddBinding(ProKeysAction.Key21, keyboard.key21);
                    AddBinding(ProKeysAction.Key22, keyboard.key22);
                    AddBinding(ProKeysAction.Key23, keyboard.key23);
                    AddBinding(ProKeysAction.Key24, keyboard.key24);

                    AddBinding(ProKeysAction.Key25, keyboard.key25);

                    AddBinding(ProKeysAction.StarPower, keyboard.overdrive);
                    AddBinding(ProKeysAction.StarPower, keyboard.selectButton);
                    AddBinding(ProKeysAction.StarPower, keyboard.digitalPedal);

                    AddBinding(ProKeysAction.TouchEffects, keyboard.touchStrip);
                    AddBinding(ProKeysAction.TouchEffects, keyboard.analogPedal);
                    return true;

                default:
                    return false;
            }
        }

        private bool SetDefaultMenuBindings(ProKeyboard keyboard)
        {
            if (!IsMenu)
                return false;

            AddBinding(MenuAction.Start, keyboard.startButton);
            AddBinding(MenuAction.Select, keyboard.selectButton);

            AddBinding(MenuAction.Green, keyboard.buttonSouth); // A, cross
            AddBinding(MenuAction.Red, keyboard.buttonEast); // B, circle
            AddBinding(MenuAction.Blue, keyboard.buttonWest); // X, square, 1
            AddBinding(MenuAction.Yellow, keyboard.buttonNorth); // Y, triangle, 2
            AddBinding(MenuAction.Orange, keyboard.overdrive);

            AddBinding(MenuAction.Up, keyboard.dpad.up);
            AddBinding(MenuAction.Down, keyboard.dpad.down);
            AddBinding(MenuAction.Left, keyboard.dpad.left);
            AddBinding(MenuAction.Right, keyboard.dpad.right);

            return true;
        }
    }
}
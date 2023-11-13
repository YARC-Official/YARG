using System;
using PlasticBand.Devices;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Input;

namespace YARG.Input
{
    public partial class BindingCollection
    {
        #region Public API
        public bool SetDefaultBindings(InputDevice device)
        {
            // Get the real device type
            return device switch
            {
                Keyboard keyboard => SetDefaultBindings(keyboard),
                Gamepad gamepad => SetDefaultBindings(gamepad),

                FiveFretGuitar guitar => SetDefaultBindings(guitar),
                SixFretGuitar guitar => SetDefaultBindings(guitar),

                FourLaneDrumkit drums => SetDefaultBindings(drums),
                FiveLaneDrumkit drums => SetDefaultBindings(drums),

                ProGuitar guitar => SetDefaultBindings(guitar),
                ProKeyboard keyboard => SetDefaultBindings(keyboard),

                // Turntable turntable => SetDefaultBindings(turntable),

                _ => false
            };
        }

        public bool SetDefaultBindings(Keyboard keyboard)
        {
            return IsMenu ? SetDefaultMenuBindings(keyboard) : SetDefaultGameplayBindings(keyboard);
        }

        public bool SetDefaultBindings(Gamepad gamepad)
        {
            return IsMenu ? SetDefaultMenuBindings(gamepad) : SetDefaultGameplayBindings(gamepad);
        }

        public bool SetDefaultBindings(FiveFretGuitar guitar)
        {
            return IsMenu ? SetDefaultMenuBindings(guitar) : SetDefaultGameplayBindings(guitar);
        }

        public bool SetDefaultBindings(SixFretGuitar guitar)
        {
            return IsMenu ? SetDefaultMenuBindings(guitar) : SetDefaultGameplayBindings(guitar);
        }

        public bool SetDefaultBindings(FourLaneDrumkit drums)
        {
            return IsMenu ? SetDefaultMenuBindings(drums) : SetDefaultGameplayBindings(drums);
        }

        public bool SetDefaultBindings(FiveLaneDrumkit drums)
        {
            return IsMenu ? SetDefaultMenuBindings(drums) : SetDefaultGameplayBindings(drums);
        }

        public bool SetDefaultBindings(ProGuitar guitar)
        {
            return IsMenu ? SetDefaultMenuBindings(guitar) : SetDefaultGameplayBindings(guitar);
        }

        public bool SetDefaultBindings(ProKeyboard keyboard)
        {
            return IsMenu ? SetDefaultMenuBindings(keyboard) : SetDefaultGameplayBindings(keyboard);
        }
        #endregion

        #region Instrument Gameplay
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
            AddBinding(GuitarAction.StarPower, guitar.tilt);

            AddBinding(GuitarAction.Whammy, guitar.whammy);

            if (guitar is GuitarHeroGuitar gh)
            {
                AddBinding(GuitarAction.StarPower, gh.spPedal);
            }
            // If we add a binding for the pickup switch, that should be set up here
            // else if (guitar is RockBandGuitar rb)
            // {
            // }

            return true;
        }

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
            AddBinding(GuitarAction.StarPower, guitar.tilt);

            AddBinding(GuitarAction.Whammy, guitar.whammy);

            return true;
        }

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
                    AddBinding(GuitarAction.StarPower, guitar.tilt);
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
                    AddBinding(ProGuitarAction.StarPower, guitar.tilt);
                    AddBinding(ProGuitarAction.StarPower, guitar.digitalPedal);
                    break;

                default:
                    return false;
            }

            return true;
        }

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
                    // AddBinding(ProKeysAction.Key1, keyboard.key1);
                    // AddBinding(ProKeysAction.Key2, keyboard.key2);
                    // AddBinding(ProKeysAction.Key3, keyboard.key3);
                    // AddBinding(ProKeysAction.Key4, keyboard.key4);
                    // AddBinding(ProKeysAction.Key5, keyboard.key5);

                    // AddBinding(ProKeysAction.Key6, keyboard.key6);
                    // AddBinding(ProKeysAction.Key7, keyboard.key7);
                    // AddBinding(ProKeysAction.Key8, keyboard.key8);
                    // AddBinding(ProKeysAction.Key9, keyboard.key9);
                    // AddBinding(ProKeysAction.Key10, keyboard.key10);
                    // AddBinding(ProKeysAction.Key11, keyboard.key11);
                    // AddBinding(ProKeysAction.Key12, keyboard.key12);

                    // AddBinding(ProKeysAction.Key13, keyboard.key13);
                    // AddBinding(ProKeysAction.Key14, keyboard.key14);
                    // AddBinding(ProKeysAction.Key15, keyboard.key15);
                    // AddBinding(ProKeysAction.Key16, keyboard.key16);
                    // AddBinding(ProKeysAction.Key17, keyboard.key17);

                    // AddBinding(ProKeysAction.Key18, keyboard.key18);
                    // AddBinding(ProKeysAction.Key19, keyboard.key19);
                    // AddBinding(ProKeysAction.Key20, keyboard.key20);
                    // AddBinding(ProKeysAction.Key21, keyboard.key21);
                    // AddBinding(ProKeysAction.Key22, keyboard.key22);
                    // AddBinding(ProKeysAction.Key23, keyboard.key23);
                    // AddBinding(ProKeysAction.Key24, keyboard.key24);

                    // AddBinding(ProKeysAction.Key25, keyboard.key25);

                    // AddBinding(ProKeysAction.StarPower, keyboard.overdrive);
                    // AddBinding(ProKeysAction.StarPower, keyboard.selectButton);
                    // AddBinding(ProKeysAction.StarPower, keyboard.digitalPedal);

                    // AddBinding(ProKeysAction.TouchEffects, keyboard.touchStrip);
                    // AddBinding(ProKeysAction.TouchEffects, keyboard.analogPedal);
                    return true;

                default:
                    return false;
            }
        }
        #endregion

        #region Instrument Menu
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
        #endregion

        #region Keyboard
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
            // AddBinding(ProKeysAction.Key1, keyboard.zKey);
            // AddBinding(ProKeysAction.Key2, keyboard.sKey);
            // AddBinding(ProKeysAction.Key3, keyboard.xKey);
            // AddBinding(ProKeysAction.Key4, keyboard.dKey);
            // AddBinding(ProKeysAction.Key5, keyboard.cKey);

            // AddBinding(ProKeysAction.Key6, keyboard.vKey);
            // AddBinding(ProKeysAction.Key7, keyboard.gKey);
            // AddBinding(ProKeysAction.Key8, keyboard.bKey);
            // AddBinding(ProKeysAction.Key9, keyboard.hKey);
            // AddBinding(ProKeysAction.Key10, keyboard.nKey);
            // AddBinding(ProKeysAction.Key11, keyboard.jKey);
            // AddBinding(ProKeysAction.Key12, keyboard.mKey);

            // AddBinding(ProKeysAction.Key13, keyboard.commaKey);
            // AddBinding(ProKeysAction.Key14, keyboard.lKey);
            // AddBinding(ProKeysAction.Key15, keyboard.periodKey);
            // AddBinding(ProKeysAction.Key16, keyboard.semicolonKey);
            // AddBinding(ProKeysAction.Key17, keyboard.slashKey);

            // // Higher keyboard
            // AddBinding(ProKeysAction.Key6, keyboard.qKey);
            // AddBinding(ProKeysAction.Key7, keyboard.digit2Key);
            // AddBinding(ProKeysAction.Key8, keyboard.wKey);
            // AddBinding(ProKeysAction.Key9, keyboard.digit3Key);
            // AddBinding(ProKeysAction.Key10, keyboard.eKey);
            // AddBinding(ProKeysAction.Key11, keyboard.digit4key);
            // AddBinding(ProKeysAction.Key12, keyboard.rKey);

            // AddBinding(ProKeysAction.Key13, keyboard.tKey);
            // AddBinding(ProKeysAction.Key14, keyboard.digit6Key);
            // AddBinding(ProKeysAction.Key15, keyboard.yKey);
            // AddBinding(ProKeysAction.Key16, keyboard.digit7Key);
            // AddBinding(ProKeysAction.Key17, keyboard.uKey);

            // AddBinding(ProKeysAction.Key18, keyboard.iKey);
            // AddBinding(ProKeysAction.Key19, keyboard.digit9Key);
            // AddBinding(ProKeysAction.Key20, keyboard.oKey);
            // AddBinding(ProKeysAction.Key21, keyboard.digit0Key);
            // AddBinding(ProKeysAction.Key22, keyboard.pKey);
            // AddBinding(ProKeysAction.Key23, keyboard.minusKey);
            // AddBinding(ProKeysAction.Key24, keyboard.leftBracketKey);

            // AddBinding(ProKeysAction.Key25, keyboard.rightBracketKey);

            // AddBinding(ProKeysAction.StarPower, keyboard.backspaceKey);

            // AddBinding(ProKeysAction.TouchEffects, keyboard.quoteKey);

            return true;
        }

        private bool SetDefaultVocalsBindings(Keyboard keyboard)
        {
            if (Mode != GameMode.Vocals)
                return false;

            AddBinding(VocalsAction.StarPower, keyboard.backspaceKey);

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
        #endregion

        #region Gamepad
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
        #endregion

        private void AddBinding<TAction>(TAction action, InputControl control)
            where TAction : unmanaged, Enum
        {
            var binding = TryGetBindingByAction(action);
            if (binding is null)
                throw new Exception($"Tried to auto-assign control {control} to action {action}, but no binding exists for it!");

            // Ignore if the control is already added, otherwise removing a device without
            // clearing any bindings to it will throw below
            if (binding.ContainsControl(control))
                return;

            bool added = binding.AddControl(control);
            if (!added)
                throw new Exception($"Could not auto-assign control {control} (type {control.GetType()}) to action {action}!");
        }

    }
}
using System.Drawing;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        public class ProKeysColors : IBinarySerializable
        {
            #region Keys

            public Color WhiteKey = Color.White;

            public Color RedKey    = Color.FromArgb(0xFF, 0x7F, 0x0E, 0x11); // #7F0E11
            public Color YellowKey = Color.FromArgb(0xFF, 0x7F, 0x74, 0x00); // #7F7400
            public Color BlueKey   = Color.FromArgb(0xFF, 0x09, 0x5F, 0x7F); // #095F7F
            public Color GreenKey  = Color.FromArgb(0xFF, 0x3C, 0x69, 0x02); // #3C6902
            public Color OrangeKey = Color.FromArgb(0xFF, 0x7F, 0x42, 0x00); // #7F4200

            /// <summary>
            /// Gets the black key color for a specific group index.
            /// 0 = red, 4 = orange.
            /// </summary>
            public Color GetBlackKeyColor(int groupIndex)
            {
                return groupIndex switch
                {
                    0 => RedKey,
                    1 => YellowKey,
                    2 => BlueKey,
                    3 => GreenKey,
                    4 => OrangeKey,
                    _ => default
                };
            }

            #endregion

            #region Overlay

            public Color RedOverlay    = DefaultRed;
            public Color YellowOverlay = DefaultYellow;
            public Color BlueOverlay   = DefaultBlue;
            public Color GreenOverlay  = DefaultGreen;
            public Color OrangeOverlay = DefaultOrange;

            /// <summary>
            /// Gets the overlay color for a specific group index.
            /// 0 = red, 4 = orange.
            /// </summary>
            public Color GetOverlayColor(int groupIndex)
            {
                return groupIndex switch
                {
                    0 => RedOverlay,
                    1 => YellowOverlay,
                    2 => BlueOverlay,
                    3 => GreenOverlay,
                    4 => OrangeOverlay,
                    _ => default
                };
            }

            #endregion

            #region Notes

            public Color WhiteNote = Color.White;
            public Color BlackNote = Color.Black;

            public Color WhiteNoteStarPower = CircularStarpower;
            public Color BlackNoteStarPower = CircularStarpower;

            #endregion

            #region Serialization

            public ProKeysColors Copy()
            {
                // Kinda yucky, but it's easier to maintain
                return (ProKeysColors) MemberwiseClone();
            }

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(WhiteKey);

                writer.Write(RedKey);
                writer.Write(YellowKey);
                writer.Write(BlueKey);
                writer.Write(GreenKey);
                writer.Write(OrangeKey);

                writer.Write(RedOverlay);
                writer.Write(YellowOverlay);
                writer.Write(BlueOverlay);
                writer.Write(GreenOverlay);
                writer.Write(OrangeOverlay);

                writer.Write(WhiteNote);
                writer.Write(BlackNote);

                writer.Write(WhiteNoteStarPower);
                writer.Write(BlackNoteStarPower);
            }

            public void Deserialize(BinaryReader reader, int version = 0)
            {
                WhiteKey = reader.ReadColor();

                RedKey = reader.ReadColor();
                YellowKey = reader.ReadColor();
                BlueKey = reader.ReadColor();
                GreenKey = reader.ReadColor();
                OrangeKey = reader.ReadColor();

                RedOverlay = reader.ReadColor();
                YellowOverlay = reader.ReadColor();
                BlueOverlay = reader.ReadColor();
                GreenOverlay = reader.ReadColor();
                OrangeOverlay = reader.ReadColor();

                WhiteNote = reader.ReadColor();
                BlackNote = reader.ReadColor();

                WhiteNoteStarPower = reader.ReadColor();
                BlackNoteStarPower = reader.ReadColor();
            }

            #endregion
        }
    }
}
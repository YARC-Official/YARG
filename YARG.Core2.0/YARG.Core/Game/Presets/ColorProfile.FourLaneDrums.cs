using System.Drawing;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        public class FourLaneDrumsColors : IFretColorProvider, IBinarySerializable
        {
            #region Frets

            public Color KickFret   = DefaultOrange;
            public Color RedFret    = DefaultRed;
            public Color YellowFret = DefaultYellow;
            public Color BlueFret   = DefaultBlue;
            public Color GreenFret  = DefaultGreen;

            /// <summary>
            /// Gets the fret color for a specific note index.
            /// 0 = kick note, 1 = red, 4 = green.
            /// </summary>
            public Color GetFretColor(int index)
            {
                return index switch
                {
                    0 => KickFret,
                    1 => RedFret,
                    2 => YellowFret,
                    3 => BlueFret,
                    4 => GreenFret,
                    _ => default
                };
            }

            public Color KickFretInner   = DefaultOrange;
            public Color RedFretInner    = DefaultRed;
            public Color YellowFretInner = DefaultYellow;
            public Color BlueFretInner   = DefaultBlue;
            public Color GreenFretInner  = DefaultGreen;

            /// <summary>
            /// Gets the inner fret color for a specific note index.
            /// 0 = kick note, 1 = red, 4 = green.
            /// </summary>
            public Color GetFretInnerColor(int index)
            {
                return index switch
                {
                    0 => KickFretInner,
                    1 => RedFretInner,
                    2 => YellowFretInner,
                    3 => BlueFretInner,
                    4 => GreenFretInner,
                    _ => default
                };
            }

            public Color KickParticles   = Color.FromArgb(0xFF, 0xFF, 0xB6, 0x00); // #FFB600
            public Color RedParticles    = DefaultRed;
            public Color YellowParticles = DefaultYellow;
            public Color BlueParticles   = DefaultBlue;
            public Color GreenParticles  = DefaultGreen;

            /// <summary>
            /// Gets the particle color for a specific note index.
            /// 0 = kick note, 1 = red, 4 = green.
            /// </summary>
            public Color GetParticleColor(int index)
            {
                return index switch
                {
                    0 => KickParticles,
                    1 => RedParticles,
                    2 => YellowParticles,
                    3 => BlueParticles,
                    4 => GreenParticles,
                    _ => default
                };
            }

            #endregion

            #region Notes

            public Color KickNote = DefaultOrange;

            public Color RedDrum    = DefaultRed;
            public Color YellowDrum = DefaultYellow;
            public Color BlueDrum   = DefaultBlue;
            public Color GreenDrum  = DefaultGreen;

            public Color RedCymbal    = DefaultRed;
            public Color YellowCymbal = DefaultYellow;
            public Color BlueCymbal   = DefaultBlue;
            public Color GreenCymbal  = DefaultGreen;

            /// <summary>
            /// Gets the note color for a specific note index.
            /// 0 = kick note, 1 = red drum, 4 = green drum, 5 = yellow cymbal.
            /// 8 is a special case: it is the red cymbal that is used in lefty-flip.
            /// </summary>
            public Color GetNoteColor(int index)
            {
                return index switch
                {
                    0 => KickNote,

                    1 => RedDrum,
                    2 => YellowDrum,
                    3 => BlueDrum,
                    4 => GreenDrum,

                    5 => YellowCymbal,
                    6 => BlueCymbal,
                    7 => GreenCymbal,
                    8 => RedCymbal,

                    _ => default
                };
            }

            public Color KickStarpower = DefaultStarpower;

            public Color RedDrumStarpower    = DefaultStarpower;
            public Color YellowDrumStarpower = DefaultStarpower;
            public Color BlueDrumStarpower   = DefaultStarpower;
            public Color GreenDrumStarpower  = DefaultStarpower;

            public Color RedCymbalStarpower    = DefaultStarpower;
            public Color YellowCymbalStarpower = DefaultStarpower;
            public Color BlueCymbalStarpower   = DefaultStarpower;
            public Color GreenCymbalStarpower  = DefaultStarpower;

            /// <summary>
            /// Gets the Star Power note color for a specific note index.
            /// 0 = kick note, 1 = red drum, 4 = green drum, 5 = yellow cymbal.
            /// 8 is a special case: it is the red cymbal that is used in lefty-flip.
            /// </summary>
            public Color GetNoteStarPowerColor(int index)
            {
                return index switch
                {
                    0 => KickStarpower,

                    1 => RedDrumStarpower,
                    2 => YellowDrumStarpower,
                    3 => BlueDrumStarpower,
                    4 => GreenDrumStarpower,

                    5 => YellowCymbalStarpower,
                    6 => BlueCymbalStarpower,
                    7 => GreenCymbalStarpower,
                    8 => RedCymbalStarpower,

                    _ => default
                };
            }

            public Color ActivationNote = DefaultPurple;

            #endregion

            #region Serialization

            public FourLaneDrumsColors Copy()
            {
                // Kinda yucky, but it's easier to maintain
                return (FourLaneDrumsColors) MemberwiseClone();
            }

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(KickFret);
                writer.Write(RedFret);
                writer.Write(YellowFret);
                writer.Write(BlueFret);
                writer.Write(GreenFret);

                writer.Write(KickFretInner);
                writer.Write(RedFretInner);
                writer.Write(YellowFretInner);
                writer.Write(BlueFretInner);
                writer.Write(GreenFretInner);

                writer.Write(KickParticles);
                writer.Write(RedParticles);
                writer.Write(YellowParticles);
                writer.Write(BlueParticles);
                writer.Write(GreenParticles);

                writer.Write(KickNote);
                writer.Write(RedDrum);
                writer.Write(YellowDrum);
                writer.Write(BlueDrum);
                writer.Write(GreenDrum);

                writer.Write(RedCymbal);
                writer.Write(YellowCymbal);
                writer.Write(BlueCymbal);
                writer.Write(GreenCymbal);

                writer.Write(KickStarpower);
                writer.Write(RedDrumStarpower);
                writer.Write(YellowDrumStarpower);
                writer.Write(BlueDrumStarpower);
                writer.Write(GreenDrumStarpower);

                writer.Write(RedCymbalStarpower);
                writer.Write(YellowCymbalStarpower);
                writer.Write(BlueCymbalStarpower);
                writer.Write(GreenCymbalStarpower);

                writer.Write(ActivationNote);
            }

            public void Deserialize(BinaryReader reader, int version = 0)
            {
                KickFret = reader.ReadColor();
                RedFret = reader.ReadColor();
                YellowFret = reader.ReadColor();
                BlueFret = reader.ReadColor();
                GreenFret = reader.ReadColor();

                KickFretInner = reader.ReadColor();
                RedFretInner = reader.ReadColor();
                YellowFretInner = reader.ReadColor();
                BlueFretInner = reader.ReadColor();
                GreenFretInner = reader.ReadColor();

                KickParticles = reader.ReadColor();
                RedParticles = reader.ReadColor();
                YellowParticles = reader.ReadColor();
                BlueParticles = reader.ReadColor();
                GreenParticles = reader.ReadColor();

                KickNote = reader.ReadColor();
                RedDrum = reader.ReadColor();
                YellowDrum = reader.ReadColor();
                BlueDrum = reader.ReadColor();
                GreenDrum = reader.ReadColor();

                RedCymbal = reader.ReadColor();
                YellowCymbal = reader.ReadColor();
                BlueCymbal = reader.ReadColor();
                GreenCymbal = reader.ReadColor();

                KickStarpower = reader.ReadColor();
                RedDrumStarpower = reader.ReadColor();
                YellowDrumStarpower = reader.ReadColor();
                BlueDrumStarpower = reader.ReadColor();
                GreenDrumStarpower = reader.ReadColor();

                RedCymbalStarpower = reader.ReadColor();
                YellowCymbalStarpower = reader.ReadColor();
                BlueCymbalStarpower = reader.ReadColor();
                GreenCymbalStarpower = reader.ReadColor();

                ActivationNote = reader.ReadColor();
            }

            #endregion
        }
    }
}
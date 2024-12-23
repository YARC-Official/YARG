using System.Drawing;
using System.IO;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile
    {
        public class FiveFretGuitarColors : IFretColorProvider, IBinarySerializable
        {
            #region Frets

            public Color OpenFret   = DefaultPurple;
            public Color GreenFret  = DefaultGreen;
            public Color RedFret    = DefaultRed;
            public Color YellowFret = DefaultYellow;
            public Color BlueFret   = DefaultBlue;
            public Color OrangeFret = DefaultOrange;

            /// <summary>
            /// Gets the fret color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public Color GetFretColor(int index)
            {
                return index switch
                {
                    1 => GreenFret,
                    2 => RedFret,
                    3 => YellowFret,
                    4 => BlueFret,
                    5 => OrangeFret,
                    (int) FiveFretGuitarFret.Open => OpenFret,
                    _ => default
                };
            }

            public Color OpenFretInner   = DefaultPurple;
            public Color GreenFretInner  = DefaultGreen;
            public Color RedFretInner    = DefaultRed;
            public Color YellowFretInner = DefaultYellow;
            public Color BlueFretInner   = DefaultBlue;
            public Color OrangeFretInner = DefaultOrange;

            /// <summary>
            /// Gets the inner fret color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public Color GetFretInnerColor(int index)
            {
                return index switch
                {
                    0 => OpenFretInner,
                    1 => GreenFretInner,
                    2 => RedFretInner,
                    3 => YellowFretInner,
                    4 => BlueFretInner,
                    5 => OrangeFretInner,
                    _ => default
                };
            }

            public Color OpenParticles   = DefaultPurple;
            public Color GreenParticles  = DefaultGreen;
            public Color RedParticles    = DefaultRed;
            public Color YellowParticles = DefaultYellow;
            public Color BlueParticles   = DefaultBlue;
            public Color OrangeParticles = DefaultOrange;

            /// <summary>
            /// Gets the particle color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public Color GetParticleColor(int index)
            {
                return index switch
                {
                    0 => OpenParticles,
                    1 => GreenParticles,
                    2 => RedParticles,
                    3 => YellowParticles,
                    4 => BlueParticles,
                    5 => OrangeParticles,
                    _ => default
                };
            }

            #endregion

            #region Notes

            public Color OpenNote   = DefaultPurple;
            public Color GreenNote  = DefaultGreen;
            public Color RedNote    = DefaultRed;
            public Color YellowNote = DefaultYellow;
            public Color BlueNote   = DefaultBlue;
            public Color OrangeNote = DefaultOrange;

            /// <summary>
            /// Gets the note color for a specific note index.
            /// 1 = green, 5 = orange.
            /// </summary>
            public Color GetNoteColor(int index)
            {
                return index switch
                {
                    1 => GreenNote,
                    2 => RedNote,
                    3 => YellowNote,
                    4 => BlueNote,
                    5 => OrangeNote,
                    (int) FiveFretGuitarFret.Open => OpenNote,
                    _ => default
                };
            }

            public Color OpenNoteStarPower   = DefaultStarpower;
            public Color GreenNoteStarPower  = DefaultStarpower;
            public Color RedNoteStarPower    = DefaultStarpower;
            public Color YellowNoteStarPower = DefaultStarpower;
            public Color BlueNoteStarPower   = DefaultStarpower;
            public Color OrangeNoteStarPower = DefaultStarpower;

            /// <summary>
            /// Gets the Star Power note color for a specific note index.
            /// 1 = green, 5 = orange.
            /// </summary>
            public Color GetNoteStarPowerColor(int index)
            {
                return index switch
                {
                    1 => GreenNoteStarPower,
                    2 => RedNoteStarPower,
                    3 => YellowNoteStarPower,
                    4 => BlueNoteStarPower,
                    5 => OrangeNoteStarPower,
                    (int) FiveFretGuitarFret.Open => OpenNoteStarPower,
                    _ => default
                };
            }

            #endregion

            #region Serialization

            public FiveFretGuitarColors Copy()
            {
                // Kinda yucky, but it's easier to maintain
                return (FiveFretGuitarColors) MemberwiseClone();
            }

            public void Serialize(BinaryWriter writer)
            {
                writer.Write(OpenFret);
                writer.Write(GreenFret);
                writer.Write(RedFret);
                writer.Write(YellowFret);
                writer.Write(BlueFret);
                writer.Write(OrangeFret);

                writer.Write(OpenFretInner);
                writer.Write(GreenFretInner);
                writer.Write(RedFretInner);
                writer.Write(YellowFretInner);
                writer.Write(BlueFretInner);
                writer.Write(OrangeFretInner);

                writer.Write(OpenParticles);
                writer.Write(GreenParticles);
                writer.Write(RedParticles);
                writer.Write(YellowParticles);
                writer.Write(BlueParticles);
                writer.Write(OrangeParticles);

                writer.Write(OpenNote);
                writer.Write(GreenNote);
                writer.Write(RedNote);
                writer.Write(YellowNote);
                writer.Write(BlueNote);
                writer.Write(OrangeNote);

                writer.Write(OpenNoteStarPower);
                writer.Write(GreenNoteStarPower);
                writer.Write(RedNoteStarPower);
                writer.Write(YellowNoteStarPower);
                writer.Write(BlueNoteStarPower);
                writer.Write(OrangeNoteStarPower);
            }

            public void Deserialize(BinaryReader reader, int version = 0)
            {
                OpenFret = reader.ReadColor();
                GreenFret = reader.ReadColor();
                RedFret = reader.ReadColor();
                YellowFret = reader.ReadColor();
                BlueFret = reader.ReadColor();
                OrangeFret = reader.ReadColor();

                OpenFretInner = reader.ReadColor();
                GreenFretInner = reader.ReadColor();
                RedFretInner = reader.ReadColor();
                YellowFretInner = reader.ReadColor();
                BlueFretInner = reader.ReadColor();
                OrangeFretInner = reader.ReadColor();

                OpenParticles = reader.ReadColor();
                GreenParticles = reader.ReadColor();
                RedParticles = reader.ReadColor();
                YellowParticles = reader.ReadColor();
                BlueParticles = reader.ReadColor();
                OrangeParticles = reader.ReadColor();

                OpenNote = reader.ReadColor();
                GreenNote = reader.ReadColor();
                RedNote = reader.ReadColor();
                YellowNote = reader.ReadColor();
                BlueNote = reader.ReadColor();
                OrangeNote = reader.ReadColor();

                OpenNoteStarPower = reader.ReadColor();
                GreenNoteStarPower = reader.ReadColor();
                RedNoteStarPower = reader.ReadColor();
                YellowNoteStarPower = reader.ReadColor();
                BlueNoteStarPower = reader.ReadColor();
                OrangeNoteStarPower = reader.ReadColor();
            }

            #endregion
        }
    }
}
using UnityEngine;

namespace YARG.Settings.Customization
{
    public partial class ColorProfile
    {
        public class FiveFretGuitarColors
        {
            #region Frets
            public Color OpenFret   = new Color32(0xC8, 0x00, 0xFF, 0xFF); // 200,   0, 255, 255
            public Color GreenFret  = new Color32(0x54, 0x98, 0x03, 0xFF); // 84,  152,   3, 255
            public Color RedFret    = new Color32(0xFF, 0x00, 0x07, 0xFF); // 255,   0,   7, 255
            public Color YellowFret = new Color32(0xFF, 0xE9, 0x00, 0xFF); // 255, 233,   0, 255
            public Color BlueFret   = new Color32(0x00, 0x72, 0x98, 0xFF); //   0, 114, 152, 255
            public Color OrangeFret = new Color32(0xFF, 0x43, 0x00, 0xFF); // 255,  67,   0, 255

            /// <summary>
            /// Gets the fret color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public Color GetFretColor(int index)
            {
                return index switch
                {
                    0 => OpenFret,
                    1 => GreenFret,
                    2 => RedFret,
                    3 => YellowFret,
                    4 => BlueFret,
                    5 => OrangeFret,
                    _ => default
                };
            }

            public Color OpenFretInner   = new Color32(0xC8, 0x00, 0xFF, 0xFF); // 200,   0, 255, 255
            public Color GreenFretInner  = new Color32(0x5F, 0xA4, 0x01, 0xFF); // 95,  164,   1, 255
            public Color RedFretInner    = new Color32(0xC8, 0x00, 0x1D, 0xFF); // 200,   0,  29, 255
            public Color YellowFretInner = new Color32(0xCA, 0xBE, 0x00, 0xFF); // 202, 190,   0, 255
            public Color BlueFretInner   = new Color32(0x01, 0x7E, 0xBB, 0xFF); //   1, 126, 187, 255
            public Color OrangeFretInner = new Color32(0xCA, 0x50, 0x00, 0xFF); // 202,  80,   0, 255

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
            #endregion

            #region Notes
            public Color OpenNote   = new Color32(0xC8, 0x00, 0xFF, 0xFF); // 200,   0, 255, 255
            public Color GreenNote  = new Color32(0x85, 0xE5, 0x00, 0xFF); // 133, 229,   0, 255
            public Color RedNote    = new Color32(0xFF, 0x03, 0x00, 0xFF); // 255,   3,   0, 255
            public Color YellowNote = new Color32(0xFF, 0xE5, 0x00, 0xFF); // 255, 229,   0, 255
            public Color BlueNote   = new Color32(0x00, 0x5D, 0xFF, 0xFF); //   0,  93, 255, 255
            public Color OrangeNote = new Color32(0xFF, 0x43, 0x00, 0xFF); // 255,  67,   0, 255

            /// <summary>
            /// Gets the note color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public Color GetNoteColor(int index)
            {
                return index switch
                {
                    0 => OpenNote,
                    1 => GreenNote,
                    2 => RedNote,
                    3 => YellowNote,
                    4 => BlueNote,
                    5 => OrangeNote,
                    _ => default
                };
            }

            public Color OpenNoteStarPower   = new Color32(0xFF, 0xFF, 0xFF, 0xFF); // 255, 255, 255, 255
            public Color GreenNoteStarPower  = new Color32(0xFF, 0xFF, 0xFF, 0xFF); // 255, 255, 255, 255
            public Color RedNoteStarPower    = new Color32(0xFF, 0xFF, 0xFF, 0xFF); // 255, 255, 255, 255
            public Color YellowNoteStarPower = new Color32(0xFF, 0xFF, 0xFF, 0xFF); // 255, 255, 255, 255
            public Color BlueNoteStarPower   = new Color32(0xFF, 0xFF, 0xFF, 0xFF); // 255, 255, 255, 255
            public Color OrangeNoteStarPower = new Color32(0xFF, 0xFF, 0xFF, 0xFF); // 255, 255, 255, 255

            /// <summary>
            /// Gets the Star Power note color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public Color GetNoteStarPowerColor(int index)
            {
                return index switch
                {
                    0 => OpenNoteStarPower,
                    1 => GreenNoteStarPower,
                    2 => RedNoteStarPower,
                    3 => YellowNoteStarPower,
                    4 => BlueNoteStarPower,
                    5 => OrangeNoteStarPower,
                    _ => default
                };
            }

            public Color OpenNoteParticles   = new Color32(0xC8, 0x00, 0xFF, 0xFF); // 200,   0, 255, 255
            public Color GreenNoteParticles  = new Color32(0x94, 0xFF, 0x00, 0xFF); // 148, 255,   0, 255
            public Color RedNoteParticles    = new Color32(0xFF, 0x00, 0x26, 0xFF); // 255,   0,  38, 255
            public Color YellowNoteParticles = new Color32(0xFF, 0xF0, 0x00, 0xFF); // 255, 240,   0, 255
            public Color BlueNoteParticles   = new Color32(0x00, 0xAB, 0xFF, 0xFF); //   0, 171, 255, 255
            public Color OrangeNoteParticles = new Color32(0xFF, 0x65, 0x00, 0xFF); // 255, 101,   0, 255

            /// <summary>
            /// Gets the particle color for a specific note index.
            /// 0 = open note, 1 = green, 5 = orange.
            /// </summary>
            public Color GetNoteParticleColor(int index)
            {
                return index switch
                {
                    0 => OpenNoteParticles,
                    1 => GreenNoteParticles,
                    2 => RedNoteParticles,
                    3 => YellowNoteParticles,
                    4 => BlueNoteParticles,
                    5 => OrangeNoteParticles,
                    _ => default
                };
            }
            #endregion
        }
    }
}
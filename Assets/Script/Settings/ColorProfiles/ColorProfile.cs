using UnityEngine;

namespace YARG.Settings.ColorProfiles
{
    public class ColorProfile
    {
        public static readonly ColorProfile Default = new();

        // TODO: Ideally, this should pull from a file with a bunch of profiles or something.
        public class FiveFretColors
        {
            public Color[] FretColors =
            {
                new Color32(200, 0, 255, 255),
                new Color32(84, 152, 3, 255),
                new Color32(255, 0, 7, 255),
                new Color32(255, 233, 0, 255),
                new Color32(0, 114, 152, 255),
                new Color32(255, 67, 0, 255),
            };

            public Color[] FretInnerColors =
            {
                new Color32(200, 0, 255, 255),
                new Color32(95, 164, 1, 255),
                new Color32(200, 0, 29, 255),
                new Color32(202, 190, 0, 255),
                new Color32(1, 126, 187, 255),
                new Color32(202, 80, 0, 255),
            };

            public Color[] NoteColors =
            {
                new Color32(200, 0, 255, 255),
                new Color32(133, 229, 0, 255),
                new Color32(255, 3, 0, 255),
                new Color32(255, 229, 0, 255),
                new Color32(0, 93, 255, 255),
                new Color32(255, 67, 0, 255),
            };

            public Color[] ParticleColors =
            {
                new Color32(200, 0, 255, 255),
                new Color32(148, 255, 0, 255),
                new Color32(255, 0, 38, 255),
                new Color32(255, 240, 0, 255),
                new Color32(0, 171, 255, 255),
                new Color32(255, 101, 0, 255),
            };

            public Color StarpowerNoteColor = Color.white;
        }

        public FiveFretColors FiveFret = new();
    }
}
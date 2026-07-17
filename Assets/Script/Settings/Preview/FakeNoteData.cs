using YARG.Themes;

namespace YARG.Settings.Preview
{
    public class FakeNoteData
    {
        public double Time;

        public int Fret;
        public bool CenterNote;
        public ThemeNoteType NoteType;

        // Overrides the global ForceStarPowerNotes toggle for this note in both
        // directions: true renders star power colors, false renders regular
        // colors, null follows the toggle. Used by the lane spotlight so the
        // edited color field (star power or not) is what's on screen.
        public bool? ForceStarPower;

        // When true, the note renders with the Miss color instead of its
        // normal color. Used by the miss-note spotlight.
        public bool ForceMiss;
    }
}
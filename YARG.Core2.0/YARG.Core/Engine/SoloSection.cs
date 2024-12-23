namespace YARG.Core.Engine
{
    public class SoloSection
    {

        public int NoteCount { get; }

        public int NotesHit { get; set; }
        
        public int SoloBonus { get; set; }

        public SoloSection(int noteCount)
        {
            NoteCount = noteCount;
        }

    }
}
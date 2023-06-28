namespace YARG.Data
{
    public class NoteInfo : AbstractInfo
    {
        /// <value>
        /// Acts as the button for a five fret, or drum pad for drums.
        /// </value>
        public int fret;

        /// <summary>
        /// Hammer-on/pull-off, or Cymbal for drums.
        /// </summary>
        public bool hopo;

        /// <summary>
        /// Tap note. Only used for five fret.
        /// </summary>
        public bool tap;

        /// <summary>
        /// Overdrive activator note on drums.
        /// </summary>
        public bool isActivator;

        /// <summary>
        /// Whether or not this HOPO is automatic.<br/>
        /// Used for difficulty downsampling.
        /// </summary>
        public bool autoHopo;

        /// <value>
        /// The fret numbers for a pro-guitar.
        /// </value>
        public int[] stringFrets;

        /// <summary>
        /// Pro-guitar mute note.
        /// </summary>
        public bool muted;

        public NoteInfo Duplicate()
        {
            return (NoteInfo) MemberwiseClone();
        }
    }
}
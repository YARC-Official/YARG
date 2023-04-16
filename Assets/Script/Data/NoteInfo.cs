namespace YARG.Data {
	public class NoteInfo : AbstractInfo {
		/// <value>
		/// Acts as the button for a five fret, or drum pad for drums.
		/// </value>
		public int fret;
		/// <summary>
		/// Hammer-on/pull-off, or Cymbal for drums.
		/// </summary>
		public bool hopo;

		/// <summary>
		/// Activates SP when hit on drums. Does not break combo if missed.
		/// </summary>
		public bool drumSPActivator;

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

		public NoteInfo Duplicate() {
			return (NoteInfo) MemberwiseClone();
		}
	}
}
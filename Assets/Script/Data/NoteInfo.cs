namespace YARG.Data {
	public class NoteInfo : AbstractInfo {
		/// <value>
		/// Acts as the button for a five fret.
		/// </value>
		public int fret;
		/// <summary>
		/// Hammer-on/pull-off.
		/// </summary>
		public bool hopo;

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
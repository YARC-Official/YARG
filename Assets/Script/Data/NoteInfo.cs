namespace YARG.Data {
	public class NoteInfo : AbstractInfo {
		/// <value>
		/// Acts as the button for five fret and the string for pro-guitar.
		/// </value>
		public int fret;
		/// <summary>
		/// Hammer-on/pull-off.
		/// </summary>
		public bool hopo;

		/// <value>
		/// The fret numbers for a pro-guitar.
		/// </value>
		public int[] stringFrets;
	}
}
using static MoonscraperChartEditor.Song.MoonNote;

namespace YARG.Chart {
	public class GuitarNote : Note {
		public int Fret     { get; }
		public int NoteMask { get; private set; }
		
		public bool IsSustain { get; }
		
		public bool IsChord => (_flags & NoteFlags.Chord) != 0;
		
		public bool IsExtendedSustain => (_flags & NoteFlags.ExtendedSustain) != 0;
		public bool IsDisjoint => (_flags & NoteFlags.Disjoint) != 0;
		
		private bool _isForced;
		
		private bool _isStrum;
		private bool _isHopo;
		private bool _isTap;

		public bool IsStrum {
			get => _isStrum;
			set {
				if (value) {
					IsHopo = false;
					IsTap = false;
				}
				_isStrum = true;
			}
		}

		public bool IsHopo {
			get => _isHopo;
			set {
				if (value) {
					IsStrum = false;
					IsTap = false;
				}
				_isHopo = true;
			}
		}

		public bool IsTap {
			get => _isTap;
			set {
				if (value) {
					IsStrum = false;
					IsHopo = false;
				}
				_isTap = true;	
			}
		}

		public GuitarNote(Note previousNote, double time, double timeLength, uint tick, uint tickLength, int fret, 
			MoonNoteType moonNoteType, NoteFlags flags) : base(previousNote, time, timeLength, tick, tickLength, flags) {
			Fret = fret;
			
			IsSustain = tickLength > 0;

			_isStrum = moonNoteType == MoonNoteType.Strum;
			_isTap = moonNoteType == MoonNoteType.Tap;
			_isHopo = moonNoteType == MoonNoteType.Hopo && !_isTap;
			
			NoteMask = 1 << fret - 1;
		}

		public override void AddChildNote(Note note) {
			if (note is not GuitarNote guitarNote)
				return;

			base.AddChildNote(note);

			NoteMask |= 1 << guitarNote.Fret - 1;
		}
	}
}
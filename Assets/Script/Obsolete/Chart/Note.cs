using System;
using System.Collections.Generic;

namespace YARG.Chart {
	public abstract class Note {
		public Note previousNote;
		public Note nextNote;

		private readonly List<Note> _childNotes;
		public IReadOnlyList<Note> ChildNotes => _childNotes;
		
		protected NoteFlags _flags;
		
		public double Time { get; }
		public double TimeLength { get; }
		
		public uint Tick { get; }
		public uint TickLength { get; }
		
		public bool IsStarPowerStart => (_flags & NoteFlags.StarPowerStart) != 0;
		public bool IsStarPower => (_flags & NoteFlags.StarPower) != 0;
		public bool IsStarPowerEnd => (_flags & NoteFlags.StarPowerEnd) != 0;

		protected Note(Note previousNote, double time, double timeLength, uint tick, uint tickLength, NoteFlags flags) {
			this.previousNote = previousNote;
			Time = time;
			TimeLength = timeLength;
			Tick = tick;
			TickLength = tickLength;

			_flags = flags;
		}

		public virtual void AddChildNote(Note note) {
			if (note.Tick != Tick || note.ChildNotes.Count > 0) {
				return;
			}
			
			_childNotes.Add(note);
		}
	}

	[Flags]
	public enum NoteFlags {
		None = 0,
		Chord = 1,
		ExtendedSustain = 2,
		Disjoint = 4,
		StarPowerStart = 8,
		StarPower = 16,
		StarPowerEnd = 32,
		SoloStart = 64,
		SoloEnd = 128,
		Cymbal = 256,
		DrumsGhost = 512,
		DrumsAccent = 1024,
	}
}


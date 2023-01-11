using System.Collections.Generic;
using UnityEngine;
using YARG.Data;

namespace YARG.Pools {
	public class NotePool : Pool {
		protected Dictionary<NoteInfo, NoteComponent> activeNotes = new();

		protected override void OnPooled(Poolable poolable) {
			if (poolable is NoteComponent noteComponent &&
				activeNotes.ContainsKey(noteComponent.noteInfo)) {
				activeNotes.Remove(noteComponent.noteInfo);
			}
		}

		public void RemoveNote(NoteInfo info) {
			if (activeNotes.TryGetValue(info, out NoteComponent note)) {
				Remove(note);
			}
		}

		public void HitNote(NoteInfo info) {
			if (activeNotes.TryGetValue(info, out NoteComponent note)) {
				note.HitNote();
			}
		}

		public void MissNote(NoteInfo info) {
			if (activeNotes.TryGetValue(info, out NoteComponent note)) {
				note.MissNote();
			}
		}

		public NoteComponent AddNote(NoteInfo info, Vector3 position) {
			var poolable = Add("note", position);
			var noteComp = (NoteComponent) poolable;
			noteComp.noteInfo = info;

			activeNotes.Add(info, noteComp);

			return noteComp;
		}
	}
}
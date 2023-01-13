using System.Collections.Generic;
using UnityEngine;

namespace YARG.Pools {
	public class NotePool : Pool {
		protected Dictionary<object, NoteComponent> activeNotes = new();

		protected override void OnPooled(Poolable poolable) {
			if (poolable is NoteComponent noteComponent &&
				activeNotes.ContainsKey(noteComponent.data)) {
				activeNotes.Remove(noteComponent.data);
			}
		}

		public void RemoveNote(object key) {
			if (activeNotes.TryGetValue(key, out NoteComponent note)) {
				Remove(note);
			}
		}

		public void HitNote(object key) {
			if (activeNotes.TryGetValue(key, out NoteComponent note)) {
				note.HitNote();
			}
		}

		public void MissNote(object key) {
			if (activeNotes.TryGetValue(key, out NoteComponent note)) {
				note.MissNote();
			}
		}

		public NoteComponent AddNote(object key, Vector3 position) {
			var poolable = Add("note", position);
			poolable.data = key;

			var noteComp = (NoteComponent) poolable;
			activeNotes.Add(key, noteComp);

			return noteComp;
		}
	}
}
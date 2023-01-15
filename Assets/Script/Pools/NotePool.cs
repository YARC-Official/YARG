using System.Collections.Generic;
using UnityEngine;

namespace YARG.Pools {
	public class NotePool : Pool {
		protected Dictionary<object, List<NoteComponent>> activeNotes = new();

		protected override void OnPooled(Poolable poolable) {
			if (poolable is NoteComponent noteComponent &&
				activeNotes.ContainsKey(noteComponent.data)) {
				activeNotes.Remove(noteComponent.data);
			}
		}

		public void RemoveNote(object key) {
			if (activeNotes.TryGetValue(key, out List<NoteComponent> list)) {
				foreach (var note in list) {
					Remove(note);
				}
			}
		}

		public void HitNote(object key) {
			if (activeNotes.TryGetValue(key, out List<NoteComponent> list)) {
				foreach (var note in list) {
					note.HitNote();
				}
			}
		}

		public void MissNote(object key) {
			if (activeNotes.TryGetValue(key, out List<NoteComponent> list)) {
				foreach (var note in list) {
					note.MissNote();
				}
			}
		}

		public NoteComponent AddNote(object key, Vector3 position) {
			var poolable = Add("note", position);
			poolable.data = key;

			var noteComp = (NoteComponent) poolable;
			if (activeNotes.TryGetValue(key, out List<NoteComponent> list)) {
				list.Add(noteComp);
			} else {
				activeNotes.Add(key, new List<NoteComponent> { noteComp });
			}

			return noteComp;
		}
	}
}
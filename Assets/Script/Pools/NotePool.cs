using System.Collections.Generic;
using UnityEngine;
using YARG.Data;

namespace YARG.Pools {
	public class NotePool : Pool {
		public float WhammyFactor { get; set; }

		protected Dictionary<NoteInfo, List<NoteComponent>> activeNotes = new();

		protected override void OnPooled(Poolable poolable) {
			if (poolable is NoteComponent noteComponent &&
				activeNotes.ContainsKey((NoteInfo) noteComponent.data)) {
				activeNotes.Remove((NoteInfo) noteComponent.data);
			}
		}

		public void RemoveNote(NoteInfo key) {
			if (activeNotes.TryGetValue(key, out List<NoteComponent> list)) {
				foreach (var note in list) {
					Remove(note);
				}
			}
		}

		public void HitNote(NoteInfo key) {
			if (activeNotes.TryGetValue(key, out List<NoteComponent> list)) {
				foreach (var note in list) {
					note.HitNote();
				}
			}
		}

		public void MissNote(NoteInfo key) {
			if (activeNotes.TryGetValue(key, out List<NoteComponent> list)) {
				foreach (var note in list) {
					note.MissNote();
				}
			}
		}

		public NoteComponent AddNote(NoteInfo key, Vector3 position) {
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
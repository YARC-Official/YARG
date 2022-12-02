using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace YARG {
	public class NotePool : MonoBehaviour {
		[SerializeField]
		private GameObject notePrefab;

		HashSet<NoteComponent> pooledNotes = new();
		Dictionary<NoteInfo, NoteComponent> activeNotes = new();

		private void Start() {
			for (int i = 0; i < 10; i++) {
				PoolNote();
			}
		}

		public void RemoveNote(NoteInfo info) {
			if (activeNotes.TryGetValue(info, out NoteComponent note)) {
				note.gameObject.SetActive(false);

				activeNotes.Remove(info);
				pooledNotes.Add(note);
			}
		}

		public void HitNote(NoteInfo info) {
			if (activeNotes.TryGetValue(info, out NoteComponent note)) {
				note.HitNote();
			}
		}

		public void RemoveNote(NoteComponent note) {
			note.gameObject.SetActive(false);

			var key = activeNotes.First(kvp => kvp.Value == note).Key;
			activeNotes.Remove(key);

			pooledNotes.Add(note);
		}

		public NoteComponent CreateNote(NoteInfo info, Vector3 localPosition) {
			// Get a note
			NoteComponent note;
			if (pooledNotes.Count <= 0) {
				note = PoolNote();
			} else {
				note = pooledNotes.First();
			}
			pooledNotes.Remove(note);

			// Activate it
			note.transform.localPosition = localPosition;
			note.gameObject.SetActive(true);

			// Add it to active notes
			activeNotes.Add(info, note);

			return note;
		}

		private NoteComponent PoolNote() {
			var go = Instantiate(notePrefab, transform);
			go.SetActive(false);

			var note = go.GetComponent<NoteComponent>();
			note.notePool = this;

			pooledNotes.Add(note);
			return note;
		}
	}
}
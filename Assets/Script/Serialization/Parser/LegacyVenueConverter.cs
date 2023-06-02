using System;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace YARG.Serialization.Parser {
	public static class LegacyVenueConverter {
		public static TrackChunk ConvertVenue(TrackChunk OldVenueTrack){
			var NewVenueEvents = new List<MidiEvent>();
			var OldVenueDict = new Dictionary<long, List<MidiEvent>>();
			var AllowedNoteOffNums = new HashSet<int> { 37, 38, 39, 40, 85, 86, 87 };
			long time = 0;

			// first, parse the current RB2 style venue for all its relevant events
			foreach(var trackEvent in OldVenueTrack.Events){
				time += trackEvent.DeltaTime;

				if(trackEvent is NoteOffEvent noteOffEvent){
					// ignore the NoteOffEvents that have pitches that are not 37-40 or 85-87
					if(!AllowedNoteOffNums.Contains(noteOffEvent.NoteNumber)) continue;
				}

				if(OldVenueDict.TryGetValue(time, out var EventList)) EventList.Add(trackEvent);
				else OldVenueDict.Add(time, new List<MidiEvent>(){ trackEvent });
			}

			// now that OldVenueDict has been populated, process these RB2 events and add them to NewVenueEvents
			long time_prev = 0;
			foreach(var oldEv in OldVenueDict){
				NewVenueEvents.AddRange(ProcessEventsAtTime(oldEv.Value, oldEv.Key - time_prev));
				time_prev = oldEv.Key;
			}
			// TODO: endoftrack event isn't appended - will this cause problems?
			// if it turns out it DOES, append it to NewVenueEvents here

			return new TrackChunk(NewVenueEvents);
		}

		private static List<MidiEvent> ProcessEventsAtTime(List<MidiEvent> list, long deltaTime) {
			var new_list = new List<MidiEvent>();
			// 8 bits, from MSB to LSB: camera parameters found
			// then, toggles for location behind, location near, location far
			// finally, toggles for vocals, guitar, drums, bass
			byte cameraInfo = 0x70;
			foreach (var ev in list) {
				if (new_list.Count > 0) deltaTime = 0;
				switch (ev) {
					case SequenceTrackNameEvent evName: new_list.Add(new SequenceTrackNameEvent() { Text = evName.Text, DeltaTime = deltaTime }); break;
					case TextEvent textEv:
						if (textEv.Text == "[verse]") new_list.Add(new TextEvent() { Text = "[lighting (verse)]", DeltaTime = deltaTime });
						else if (textEv.Text == "[chorus]") new_list.Add(new TextEvent() { Text = "[lighting (chorus)]", DeltaTime = deltaTime });
						else if (textEv.Text == "[lighting ()]") new_list.Add(new TextEvent() { Text = "[lighting (harmony)]", DeltaTime = deltaTime });
						else if (textEv.Text.Contains("do_directed_cut ")) {
							string new_text = textEv.Text.Replace("do_directed_cut ", "");
							if (new_text == "[directed_vocals_cam]") new_text = "[directed_vocals_cam_pt]";
							if (new_text == "[directed_guitar_cam]") new_text = "[directed_guitar_cam_pt]";
							new_list.Add(new TextEvent() { Text = new_text, DeltaTime = deltaTime });
						}
						else new_list.Add(new TextEvent() { Text = textEv.Text, DeltaTime = deltaTime });
						break;
					case NoteOnEvent noteOnEv:
						switch (noteOnEv.NoteNumber) {
							case 37: case 38: case 39: case 40: case 85: case 86: case 87:
								new_list.Add(new NoteOnEvent() { NoteNumber = noteOnEv.NoteNumber, Velocity = noteOnEv.Velocity, Channel = noteOnEv.Channel, DeltaTime = deltaTime });
								break;
							case 48: new_list.Add(new TextEvent() { Text = "[next]", DeltaTime = deltaTime }); break;
							case 49: new_list.Add(new TextEvent() { Text = "[prev]", DeltaTime = deltaTime }); break;
							case 50: new_list.Add(new TextEvent() { Text = "[first]", DeltaTime = deltaTime }); break;
							case 60: cameraInfo |= 0x80; break; // enable the "camera parameters found" bit
							case 61: cameraInfo |= 0x81; break; // ditto, plus the "bass focus" bit
							case 62: cameraInfo |= 0x82; break; // plus the "drums focus" bit
							case 63: cameraInfo |= 0x84; break; // plus the "guitar focus" bit
							case 64: cameraInfo |= 0x88; break; // plus the "vocals focus" bit
							// then, toggles for location behind, location near, location far
							case 70: cameraInfo = (byte)((cameraInfo | 0x80) & 0xBF); break; // zero out the 7th bit (behind = false)
							case 71: cameraInfo = (byte)((cameraInfo | 0x80) & 0x9F); break; // zero out the 7th and 6th bits (behind = false, near = false)
							case 72: cameraInfo = (byte)((cameraInfo | 0x80) & 0xAF); break; // zero out the 7th and 5th bits (behind = false, far = false)
							case 73: cameraInfo = (byte)((cameraInfo | 0x80) & 0xDF); break; // zero out the 6th bit (near = false)
							case 96: new_list.Add(new TextEvent() { Text = "[ProFilm_a.pp]", DeltaTime = deltaTime }); break;
							case 97: new_list.Add(new TextEvent() { Text = "[contrast_a.pp]", DeltaTime = deltaTime }); break;
							case 98: new_list.Add(new TextEvent() { Text = "[film_16mm.pp]", DeltaTime = deltaTime }); break;
							case 99: new_list.Add(new TextEvent() { Text = "[film_sepia_ink.pp]", DeltaTime = deltaTime }); break;
							case 100: new_list.Add(new TextEvent() { Text = "[film_silvertone.pp]", DeltaTime = deltaTime }); break;
							case 101: new_list.Add(new TextEvent() { Text = "[photo_negative.pp]", DeltaTime = deltaTime }); break;
							case 102: new_list.Add(new TextEvent() { Text = "[photocopy.pp]", DeltaTime = deltaTime }); break;
							case 103: new_list.Add(new TextEvent() { Text = "[ProFilm_a.pp]", DeltaTime = deltaTime }); break;
							case 104: new_list.Add(new TextEvent() { Text = "[ProFilm_b.pp]", DeltaTime = deltaTime }); break;
							case 105: new_list.Add(new TextEvent() { Text = "[ProFilm_mirror_a.pp]", DeltaTime = deltaTime }); break;
							case 106: new_list.Add(new TextEvent() { Text = "[film_blue_filter.pp]", DeltaTime = deltaTime }); break;
							case 107: new_list.Add(new TextEvent() { Text = "[video_a.pp]", DeltaTime = deltaTime }); break;
							case 108: new_list.Add(new TextEvent() { Text = "[video_bw.pp]", DeltaTime = deltaTime }); break;
							case 109: new_list.Add(new TextEvent() { Text = "[video_security.pp]", DeltaTime = deltaTime }); break;
							case 110: new_list.Add(new TextEvent() { Text = "[video_trails.pp]", DeltaTime = deltaTime }); break;
							default: throw new Exception($"Invalid note pitch {noteOnEv.NoteNumber} found!");
						}
						break;
					case NoteOffEvent noteOffEv:
						new_list.Add(new NoteOffEvent() { NoteNumber = noteOffEv.NoteNumber, Velocity = noteOffEv.Velocity, Channel = noteOffEv.Channel, DeltaTime = deltaTime });
						break;
					default: throw new Exception($"Invalid MIDI event {ev} found!");
				}
			}

			// this is the part where you add a camera cut text event based off the note_ons you found
			List<string> textsFromNotes = GetCameraCutFromNotes(cameraInfo);
			foreach (var note in textsFromNotes) {
				if (new_list.Count > 0) deltaTime = 0;
				new_list.Add(new TextEvent() { Text = note, DeltaTime = deltaTime });
			}

			return new_list;
		}

		private static List<string> GetCameraCutFromNotes(byte notes) {
			byte instruments = (byte)(notes & 0xF); // from MSB to LSB: vox, guitar, drums, bass
			bool cameraParametersFound = (notes & 0x80) == 0x80;
			bool behind = (notes & 0x40) == 0x40;
			bool near = (notes & 0x20) == 0x20;
			bool far = (notes & 0x10) == 0x10;
			var newTextEvents = new List<string>();

			switch (instruments) {
				case 0x0: // no focus on any particular instrument - make sure any other camera relevant notes were also called!
					if (cameraParametersFound) {
						if (behind) newTextEvents.Add("[coop_all_behind]");
						if (far) newTextEvents.Add("[coop_all_far]");
						if (near) newTextEvents.Add("[coop_all_near]");
					}
					break;
				case 0x1: // focus on the bass ONLY
					if (behind) newTextEvents.Add("[coop_b_behind]");
					if (near || far) {
						if (!behind && !far) newTextEvents.Add("[coop_b_closeup_hand]");
						else newTextEvents.Add("[coop_b_near]");
					}
					break;
				case 0x2: // focus on the drums ONLY
					if (behind) newTextEvents.Add("[coop_d_behind]");
					if (near || far) {
						if (!behind && !far) newTextEvents.Add("[coop_d_closeup_hand]");
						else newTextEvents.Add("[coop_d_near]");
					}
					break;
				case 0x3: // focus on bass AND drums
					if (near || far) newTextEvents.Add("[coop_bd_near]");
					break;
				case 0x4: // focus on the guitar ONLY
					if (behind) newTextEvents.Add("[coop_g_behind]");
					if (near || far) {
						if (!behind && !far) newTextEvents.Add("[coop_g_closeup_hand]");
						else newTextEvents.Add("[coop_g_near]");
					}
					break;
				case 0x5: // focus on bass AND guitar
					if (behind) newTextEvents.Add("[coop_bg_behind]");
					if (near || far) newTextEvents.Add("[coop_bg_near]");
					break;
				case 0x6: // focus on drums AND guitar
					if (near || far) newTextEvents.Add("[coop_dg_near]");
					break;
				case 0x8: // focus on the vocals ONLY
					if (behind) newTextEvents.Add("[coop_v_behind]");
					if (near || far) {
						if (!behind && !far) newTextEvents.Add("[coop_v_closeup]");
						else newTextEvents.Add("[coop_v_near]");
					}
					break;
				case 0x9: // focus on bass AND vocals
					if (behind) newTextEvents.Add("[coop_bv_behind]");
					if (near || far) newTextEvents.Add("[coop_bv_near]");
					break;
				case 0xA: // focus on drums AND vocals
					if (near || far) newTextEvents.Add("[coop_dv_near]");
					break;
				case 0xC: // focus on guitar AND vocals
					if (behind) newTextEvents.Add("[coop_gv_behind]");
					if (near || far) newTextEvents.Add("[coop_gv_near]");
					break;
				case 0xD: // focus on every instrument except drums
					if (behind) newTextEvents.Add("[coop_front_behind]");
					if (near || far) newTextEvents.Add("[coop_front_near]");
					break;
				case 0xF: // focus on every instrument
					if (behind) newTextEvents.Add("[coop_all_behind]");
					if (far) newTextEvents.Add("[coop_all_far]");
					if (near) newTextEvents.Add("[coop_all_near]");
					break;
				default: throw new Exception("Encountered an invalid combination of focused instruments!");
			}

			return newTextEvents;
		}
	}
}
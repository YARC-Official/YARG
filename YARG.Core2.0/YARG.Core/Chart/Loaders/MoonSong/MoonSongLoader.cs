using System;
using System.Collections.Generic;
using System.IO;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using YARG.Core.Logging;

namespace YARG.Core.Chart
{
    using CurrentPhrases = Dictionary<MoonPhrase.Type, MoonPhrase>;

    /// <summary>
    /// Loads chart data from a MoonSong.
    /// </summary>
    internal partial class MoonSongLoader : ISongLoader
    {
        private delegate TNote CreateNoteDelegate<TNote>(MoonNote moonNote, CurrentPhrases currentPhrases)
            where TNote : Note<TNote>;
        private delegate void ProcessTextDelegate(MoonText text);

        private MoonSong _moonSong;
        private ParseSettings _settings;

        private GameMode _currentMode;
        private Instrument _currentInstrument;
        private Difficulty _currentDifficulty;

        private MoonChart.GameMode _currentMoonMode;
        private MoonSong.MoonInstrument _currentMoonInstrument;
        private MoonSong.Difficulty _currentMoonDifficulty;

        public MoonSongLoader(MoonSong song, in ParseSettings settings)
        {
            _moonSong = song;
            _settings = settings;
        }

        public static MoonSongLoader LoadSong(ParseSettings settings, string filePath)
        {
            var song = Path.GetExtension(filePath).ToLower() switch
            {
                ".mid" => MidReader.ReadMidi(ref settings, filePath),
                ".chart" => ChartReader.ReadFromFile(ref settings, filePath),
                _ => throw new ArgumentException($"Unrecognized file extension for chart path '{filePath}'!", nameof(filePath))
            };

            return new(song, settings);
        }

        public static MoonSongLoader LoadMidi(ParseSettings settings, MidiFile midi)
        {
            var song = MidReader.ReadMidi(ref settings, midi);
            return new(song, settings);
        }

        public static MoonSongLoader LoadDotChart(ParseSettings settings, string chartText)
        {
            var song = ChartReader.ReadFromText(ref settings, chartText);
            return new(song, settings);
        }

        public List<TextEvent> LoadGlobalEvents()
        {
            var textEvents = new List<TextEvent>(_moonSong.events.Count);
            foreach (var moonText in _moonSong.events)
            {
                double time = _moonSong.TickToTime(moonText.tick);
                var newText = new TextEvent(moonText.text, time, moonText.tick);
                textEvents.Add(newText);
            }

            return textEvents;
        }

        public List<Section> LoadSections()
        {
            var sections = new List<Section>();

            for (var i = 0; i < _moonSong.sections.Count;)
            {
                var moonSection = _moonSong.sections[i];
                double time = _moonSong.TickToTime(moonSection.tick);
                sections.Add(new Section(moonSection.text, time, moonSection.tick));

                if (++i < _moonSong.sections.Count)
                {
                    double next = _moonSong.TickToTime(_moonSong.sections[i].tick);
                    sections[^1].TimeLength = next - time;
                    sections[^1].TickLength = _moonSong.sections[i].tick - moonSection.tick;
                }
            }

            return sections;
        }

        public SyncTrack LoadSyncTrack()
        {
            return _moonSong.syncTrack;
        }

        private InstrumentDifficulty<TNote> LoadDifficulty<TNote>(Instrument instrument, Difficulty difficulty,
            CreateNoteDelegate<TNote> createNote, ProcessTextDelegate? processText = null)
            where TNote : Note<TNote>
        {
            _currentMode = instrument.ToGameMode();
            _currentInstrument = instrument;
            _currentDifficulty = difficulty;

            _currentMoonMode = YargGameModeToMoonGameMode(_currentMode);
            _currentMoonInstrument = YargInstrumentToMoonInstrument(_currentInstrument);
            _currentMoonDifficulty = YargDifficultyToMoonDifficulty(_currentDifficulty);

            var moonChart = GetMoonChart(instrument, difficulty);
            var notes = GetNotes(moonChart, difficulty, createNote, processText);
            var phrases = GetPhrases(moonChart);
            var textEvents = GetTextEvents(moonChart);
            return new(instrument, difficulty, notes, phrases, textEvents);
        }

        private List<TNote> GetNotes<TNote>(MoonChart moonChart, Difficulty difficulty,
            CreateNoteDelegate<TNote> createNote, ProcessTextDelegate? processText = null)
            where TNote : Note<TNote>
        {
            var notes = new List<TNote>(moonChart.notes.Count);

            int moonPhraseIndex = 0;
            int moonTextIndex = 0;

            // Phrases stored here are *not* guaranteed to be active, as it's simpler that way
            // We need to check the phrase bounds anyways, which is very simple to do
            var currentPhrases = new CurrentPhrases();

            foreach (var moonNote in moonChart.notes)
            {
                // Keep track of active special phrases
                while (moonPhraseIndex < moonChart.specialPhrases.Count)
                {
                    var moonPhrase = moonChart.specialPhrases[moonPhraseIndex];
                    if (moonPhrase.tick > moonNote.tick)
                        break;

                    currentPhrases[moonPhrase.type] = moonPhrase;
                    moonPhraseIndex++;
                }

                // Send through text events, if requested
                if (processText != null)
                {
                    while (moonTextIndex < moonChart.events.Count)
                    {
                        var moonText = moonChart.events[moonTextIndex];
                        if (moonText.tick > moonNote.tick)
                            break;

                        processText(moonText);
                        moonTextIndex++;
                    }
                }

                // Skip Expert+ notes if not on Expert+
                if (difficulty != Difficulty.ExpertPlus && (moonNote.flags & MoonNote.Flags.InstrumentPlus) != 0)
                    continue;

                var newNote = createNote(moonNote, currentPhrases);
                AddNoteToList(notes, newNote);
            }

            notes.TrimExcess();
            return notes;
        }

        private List<Phrase> GetPhrases(MoonChart moonChart)
        {
            var phrases = new List<Phrase>(moonChart.specialPhrases.Count);
            foreach (var moonPhrase in moonChart.specialPhrases)
            {
                PhraseType? phraseType = moonPhrase.type switch
                {
                    MoonPhrase.Type.Starpower           => PhraseType.StarPower,
                    MoonPhrase.Type.Solo                => PhraseType.Solo,
                    MoonPhrase.Type.Versus_Player1      => PhraseType.VersusPlayer1,
                    MoonPhrase.Type.Versus_Player2      => PhraseType.VersusPlayer2,
                    MoonPhrase.Type.TremoloLane         => PhraseType.TremoloLane,
                    MoonPhrase.Type.TrillLane           => PhraseType.TrillLane,
                    MoonPhrase.Type.ProDrums_Activation => PhraseType.DrumFill,

                    MoonPhrase.Type.ProKeys_RangeShift0 => PhraseType.ProKeys_RangeShift0,
                    MoonPhrase.Type.ProKeys_RangeShift1 => PhraseType.ProKeys_RangeShift1,
                    MoonPhrase.Type.ProKeys_RangeShift2 => PhraseType.ProKeys_RangeShift2,
                    MoonPhrase.Type.ProKeys_RangeShift3 => PhraseType.ProKeys_RangeShift3,
                    MoonPhrase.Type.ProKeys_RangeShift4 => PhraseType.ProKeys_RangeShift4,
                    MoonPhrase.Type.ProKeys_RangeShift5 => PhraseType.ProKeys_RangeShift5,

                    _ => null
                };

                if (!phraseType.HasValue)
                    continue;

                double time = _moonSong.TickToTime(moonPhrase.tick);
                var newPhrase = new Phrase(phraseType.Value, time, GetLengthInTime(moonPhrase), moonPhrase.tick, moonPhrase.length);
                phrases.Add(newPhrase);
            }

            return phrases;
        }

        private List<TextEvent> GetTextEvents(MoonChart moonChart)
        {
            var textEvents = new List<TextEvent>(moonChart.events.Count);
            foreach (var moonText in moonChart.events)
            {
                double time = _moonSong.TickToTime(moonText.tick);
                var newText = new TextEvent(moonText.text, time, moonText.tick);
                textEvents.Add(newText);
            }

            return textEvents;
        }

        private NoteFlags GetGeneralFlags(MoonNote moonNote, CurrentPhrases currentPhrases)
        {
            var flags = NoteFlags.None;

            var previous = moonNote.PreviousSeperateMoonNote;
            var next = moonNote.NextSeperateMoonNote;

            // Star power
            if (currentPhrases.TryGetValue(MoonPhrase.Type.Starpower, out var starPower) && IsEventInPhrase(moonNote, starPower))
            {
                flags |= NoteFlags.StarPower;

                if (previous == null || !IsEventInPhrase(previous, starPower))
                    flags |= NoteFlags.StarPowerStart;

                if (next == null || !IsEventInPhrase(next, starPower))
                    flags |= NoteFlags.StarPowerEnd;
            }

            // Solos
            if (currentPhrases.TryGetValue(MoonPhrase.Type.Solo, out var solo) && IsEventInPhrase(moonNote, solo))
            {
                flags |= NoteFlags.Solo;

                if (previous == null || !IsEventInPhrase(previous, solo))
                    flags |= NoteFlags.SoloStart;

                if (next == null || !IsEventInPhrase(next, solo))
                    flags |= NoteFlags.SoloEnd;
            }

            return flags;
        }

        private void AddNoteToList<TNote>(List<TNote> notes, TNote note)
            where TNote : Note<TNote>
        {
            // The parent of all notes on the current tick
            var currentParent = notes.Count > 0 ? notes[^1] : null;
            // Previous parent note (on a different tick)
            var previousParent = notes.Count > 1 ? notes[^2] : null;

            // Determine if this is part of a chord
            if (currentParent != null)
            {
                if (note.Tick == currentParent.Tick)
                {
                    // Same chord, assign previous and add as child
                    note.PreviousNote = previousParent;
                    currentParent.AddChildNote(note);
                    return;
                }
                else if ((note.Tick - currentParent.Tick) <= _settings.NoteSnapThreshold)
                {
                    // Chord needs to be snapped, copy values
                    note.CopyValuesFrom(currentParent);

                    note.PreviousNote = previousParent;
                    currentParent.AddChildNote(note);
                    return;
                }
            }

            // New chord
            previousParent = currentParent;
            currentParent = note;

            // Assign next/previous note references
            if (previousParent is not null)
            {
                previousParent.NextNote = currentParent;
                foreach (var child in previousParent.ChildNotes)
                    child.NextNote = currentParent;

                currentParent.PreviousNote = previousParent;
            }

            notes.Add(note);
        }

        private double GetLengthInTime(double startTime, uint tick, uint tickLength)
        {
            return _moonSong.TickToTime(tick + tickLength) - startTime;
        }

        private double GetLengthInTime(MoonNote note)
        {
            double time = _moonSong.TickToTime(note.tick);
            return GetLengthInTime(time, note.tick, note.length);
        }

        private double GetLengthInTime(MoonPhrase phrase)
        {
            double time = _moonSong.TickToTime(phrase.tick);
            return GetLengthInTime(time, phrase.tick, phrase.length);
        }

        private static bool IsEventInPhrase(MoonObject songObj, MoonPhrase phrase)
        {
            if (songObj == null || phrase == null)
            {
                YargLogger.Assert(songObj != null);
                YargLogger.Assert(phrase != null);
                return false;
            }

            // Ensure 0-length phrases still take effect
            // (e.g. the SP phrases at the end of ExileLord - Hellidox)
            if (phrase.length == 0)
                return songObj.tick == phrase.tick;

            return phrase.tick <= songObj.tick && songObj.tick < (phrase.tick + phrase.length);
        }

        private static bool IsNoteClosestToEndOfPhrase(MoonSong song, MoonNote note, MoonPhrase phrase)
        {
            int endTick = (int) (phrase.tick + phrase.length);

            // Find the note to compare against
            MoonNote otherNote;
            {
                var previousNote = note.PreviousSeperateMoonNote;
                var nextNote = note.NextSeperateMoonNote;

                if (IsEventInPhrase(note, phrase))
                {
                    // Note is in the phrase, check if this is the last note in the phrase
                    if (nextNote is not null && !IsEventInPhrase(nextNote, phrase))
                    {
                        // The phrase ends between the given note and the next note
                        otherNote = nextNote;
                    }
                    else
                    {
                        // This is either the last note in the chart, or not the last note of the phrase
                        return nextNote is null;
                    }
                }
                else
                {
                    // Note is not in the phrase, check if the previous note is the last in the phrase
                    if (previousNote is null)
                    {
                        // This is the first note in the chart, check by distance
                        uint tickThreshold = song.resolution / 3; // 1/12th note
                        return Math.Abs((int) note.tick - endTick) < tickThreshold;
                    }
                    else if (note.tick >= endTick && previousNote.tick < endTick)
                    {
                        // The phrase ends between the previous note and the given note
                        // IsEventInPhrase() is not used here since cases such as drum activations at the end of breaks
                        // can possibly make it so that neither the previous nor given note are in the phrase
                        otherNote = previousNote;
                    }
                    else
                    {
                        // The phrase is not applicable to the given note
                        return false;
                    }
                }
            }

            // Compare the distance of each note
            // If the distances are equal, the previous note wins
            int currentDistance = Math.Abs((int) note.tick - endTick);
            int otherDistance = Math.Abs((int) otherNote.tick - endTick);
            return currentDistance < otherDistance || (currentDistance == otherDistance && note.tick < otherNote.tick);
        }

        private MoonChart GetMoonChart(Instrument instrument, Difficulty difficulty)
        {
            var moonInstrument = YargInstrumentToMoonInstrument(instrument);
            var moonDifficulty = YargDifficultyToMoonDifficulty(difficulty);
            return _moonSong.GetChart(moonInstrument, moonDifficulty);
        }

        private static MoonChart.GameMode YargGameModeToMoonGameMode(GameMode mode) => mode switch
        {
            GameMode.FiveFretGuitar => MoonChart.GameMode.Guitar,
            GameMode.SixFretGuitar => MoonChart.GameMode.GHLGuitar,

            GameMode.FourLaneDrums => MoonChart.GameMode.Drums,
            GameMode.FiveLaneDrums => MoonChart.GameMode.Drums,

            GameMode.ProGuitar => MoonChart.GameMode.ProGuitar,
            GameMode.ProKeys => MoonChart.GameMode.ProKeys,

            GameMode.Vocals => MoonChart.GameMode.Vocals,

            _ => throw new NotImplementedException($"Unhandled game mode {mode}!")
        };

        private static MoonSong.MoonInstrument YargInstrumentToMoonInstrument(Instrument instrument) => instrument switch
        {
            Instrument.FiveFretGuitar     => MoonSong.MoonInstrument.Guitar,
            Instrument.FiveFretCoopGuitar => MoonSong.MoonInstrument.GuitarCoop,
            Instrument.FiveFretBass       => MoonSong.MoonInstrument.Bass,
            Instrument.FiveFretRhythm     => MoonSong.MoonInstrument.Rhythm,
            Instrument.Keys               => MoonSong.MoonInstrument.Keys,

            Instrument.SixFretGuitar     => MoonSong.MoonInstrument.GHLiveGuitar,
            Instrument.SixFretCoopGuitar => MoonSong.MoonInstrument.GHLiveBass,
            Instrument.SixFretBass       => MoonSong.MoonInstrument.GHLiveRhythm,
            Instrument.SixFretRhythm     => MoonSong.MoonInstrument.GHLiveCoop,

            Instrument.FourLaneDrums or
            Instrument.FiveLaneDrums or
            Instrument.ProDrums => MoonSong.MoonInstrument.Drums,

            Instrument.ProGuitar_17Fret => MoonSong.MoonInstrument.ProGuitar_17Fret,
            Instrument.ProGuitar_22Fret => MoonSong.MoonInstrument.ProGuitar_22Fret,
            Instrument.ProBass_17Fret   => MoonSong.MoonInstrument.ProBass_17Fret,
            Instrument.ProBass_22Fret   => MoonSong.MoonInstrument.ProBass_22Fret,

            Instrument.ProKeys => MoonSong.MoonInstrument.ProKeys,

            // Vocals and harmony need to be handled specially
            // Instrument.Vocals  => MoonSong.MoonInstrument.Vocals,
            // Instrument.Harmony => MoonSong.MoonInstrument.Harmony1,

            _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
        };

        private static MoonSong.Difficulty YargDifficultyToMoonDifficulty(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Easy       => MoonSong.Difficulty.Easy,
            Difficulty.Medium     => MoonSong.Difficulty.Medium,
            Difficulty.Hard       => MoonSong.Difficulty.Hard,
            Difficulty.Expert or
            Difficulty.ExpertPlus => MoonSong.Difficulty.Expert,
            _ => throw new InvalidOperationException($"Invalid difficulty {difficulty}!")
        };
    }
}
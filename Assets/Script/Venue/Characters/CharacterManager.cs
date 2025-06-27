using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Gameplay;

namespace YARG.Venue.Characters
{
    public class CharacterManager : GameplayBehaviour
    {

        [SerializeField]
        private GameObject _venue;

        private Dictionary<VenueCharacter.CharacterType, VenueCharacter> _characters = new();

        // Ugh, the different note types ruin me again
        private List<VocalsPhrase> _vocalNotes;
        private List<DrumNote> _drumNotes;
        private List<GuitarNote> _guitarNotes;
        private List<GuitarNote> _bassNotes;
        private List<GuitarNote> _keysNotes;
        private List<ProKeysNote> _proKeysNotes;

        private int _guitarNoteIndex;
        private int _bassNoteIndex;
        private int _keysNoteIndex;
        private int _proKeysNoteIndex;
        private int _drumNoteIndex;
        private int _vocalNoteIndex;

        private List<TempoChange> _tempoList;
        private int               _tempoIndex;
        private int               _previousTempoIndex;
        private TempoChange       _currentTempo;

        protected override void OnChartLoaded(SongChart chart)
        {
            // Find all the VenueCharacters in the venue

            var _venueCharacters = _venue.GetComponentsInChildren<VenueCharacter>(true);

            foreach (var character in _venueCharacters)
            {
                _characters.Add(character.Type, character);
            }

            // Get the expert notes for each track
            var guitarId = chart.FiveFretGuitar.GetDifficulty(Difficulty.Expert);
            var bassId = chart.FiveFretBass.GetDifficulty(Difficulty.Expert);
            var keysId = chart.Keys.GetDifficulty(Difficulty.Expert);
            var proKeysId = chart.ProKeys.GetDifficulty(Difficulty.Expert);
            var vocalsId = chart.Vocals.Parts[0];
            var drumsId = chart.ProDrums.GetDifficulty(Difficulty.Expert);

            _guitarNotes = guitarId.Notes;
            _bassNotes = bassId.Notes;
            _keysNotes = keysId.Notes;
            _proKeysNotes = proKeysId.Notes;
            _vocalNotes = vocalsId.NotePhrases;
            _drumNotes = drumsId.Notes;

            _tempoList = chart.SyncTrack.Tempos;

            // Don't animate until there are notes (at least until we have idle animations)
            foreach (var character in _characters.Values)
            {
                character.StopAnimation();
            }
        }

        private void Update()
        {
            bool tempoChanged = false;

            while (_tempoList.Count > 0 && _tempoIndex < _tempoList.Count &&
                _tempoList[_tempoIndex].Time <= GameManager.SongTime)
            {
                _currentTempo = _tempoList[_tempoIndex];
                tempoChanged = true;
                _tempoIndex++;
            }

            // For each character, use its type to determine which track it is on, then trigger an animation if the note type is correct for that
            foreach (var key in _characters.Keys)
            {
                var character = _characters[key];

                if (tempoChanged)
                {
                    character.UpdateTempo(_currentTempo.SecondsPerBeat);
                }
                switch (key)
                {
                    case VenueCharacter.CharacterType.Guitar:
                        ProcessGuitar(character);
                        break;
                    case VenueCharacter.CharacterType.Bass:
                        ProcessBass(character);
                        break;
                    case VenueCharacter.CharacterType.Vocals:
                        ProcessVocals(character);
                        break;
                    case VenueCharacter.CharacterType.Drums:
                        ProcessDrums(character);
                        break;
                }
            }
        }

        // TODO: Fix it so the lookahead for moving to the playing animation doesn't also cause us to switch
        // into the idle animation early

        private void ProcessGuitar(VenueCharacter character)
        {
            while (_guitarNotes.Count > 0 && _guitarNoteIndex < _guitarNotes.Count && _guitarNotes[_guitarNoteIndex].Time - character.TimeToFirstHit <= GameManager.SongTime + character.TimeToFirstHit)
            {
                if (_guitarNoteIndex >= _guitarNotes.Count)
                {
                    break;
                }

                var note = _guitarNotes[_guitarNoteIndex];
                _guitarNoteIndex++;

                if (!character.IsAnimating())
                {
                    character.StartAnimation(_currentTempo.SecondsPerBeat);
                    return;
                }

                // If next note is more than secondsPerBeat away, stop animating
                if (note.NextNote == null || note.NextNote.Time - character.TimeToFirstHit > GameManager.SongTime + (_currentTempo.SecondsPerBeat * 2))
                {
                    // TODO: This actually needs to happen character.TimeToFirstHit after this point
                    character.StopAnimation();
                }

                // Trigger the strum animation
                // if (note.IsStrum)
                // {
                //     character.OnNote(note);
                // }
            }
        }

        private void ProcessBass(VenueCharacter character)
        {
            while (_bassNotes.Count > 0 && _bassNoteIndex < _bassNotes.Count && _bassNotes[_bassNoteIndex].Time - character.TimeToFirstHit <= GameManager.SongTime)
            {
                if (_bassNoteIndex >= _bassNotes.Count)
                {
                    break;
                }

                var note = _bassNotes[_bassNoteIndex];
                _bassNoteIndex++;

                if (!character.IsAnimating())
                {
                    character.StartAnimation(_currentTempo.SecondsPerBeat);
                    return;
                }

                // If next note is more than secondsPerBeat away, stop animating
                if (note.NextNote == null || note.NextNote.Time > GameManager.SongTime + _currentTempo.SecondsPerBeat * 2)
                {
                    character.StopAnimation();
                }


                // Trigger the strum animation
                // if (note.IsStrum)
                // {
                //     character.OnNote(note);
                // }
            }
        }

        // TODO: Figure out something reasonable to do for the vocalist
        private void ProcessVocals(VenueCharacter character)
        {

        }

        private void ProcessDrums(VenueCharacter character)
        {
            while (_drumNotes.Count > 0 && _drumNoteIndex < _drumNotes.Count &&
                _drumNotes[_drumNoteIndex].Time - character.TimeToFirstHit <= GameManager.SongTime)
            {
                if (_drumNoteIndex >= _drumNotes.Count)
                {
                    break;
                }
                var note = _drumNotes[_drumNoteIndex];
                _drumNoteIndex++;

                if (!character.IsAnimating())
                {
                    character.StartAnimation(_currentTempo.SecondsPerBeat);
                    return;
                }

                // If next note is more than secondsPerBeat away, stop animating
                if (note.NextNote == null || note.NextNote.Time > GameManager.SongTime + _currentTempo.SecondsPerBeat * 2)
                {
                    character.StopAnimation();
                }

                // character.OnNote(note);
            }
        }
    }
}
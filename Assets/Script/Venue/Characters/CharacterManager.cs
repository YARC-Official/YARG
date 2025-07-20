using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Gameplay;
using AnimationEvent = YARG.Core.Chart.AnimationEvent;

namespace YARG.Venue.Characters
{
    public class CharacterManager : GameplayBehaviour
    {

        [SerializeField]
        private GameObject _venue;

        private Dictionary<VenueCharacter.CharacterType, VenueCharacter> _characters = new();

        // Ugh, the different note types ruin me again
        private List<VocalsPhrase> _vocalNotes;
        private List<DrumNote>     _drumNotes;
        private List<GuitarNote>   _guitarNotes;
        private List<GuitarNote>   _bassNotes;
        private List<GuitarNote>   _keysNotes;
        private List<ProKeysNote>  _proKeysNotes;
        private List<TextEvent>    _guitarTextEvents;
        private List<TextEvent>    _bassTextEvents;
        private List<TextEvent>    _drumTextEvents;
        private List<TextEvent>    _keysTextEvents;
        private List<TextEvent>    _proKeysTextEvents;
        private List<TextEvent>    _vocalTextEvents;

        private List<AnimationEvent> _guitarAnimationEvents;
        private List<AnimationEvent> _bassAnimationEvents;
        private List<AnimationEvent> _drumAnimationEvents;

        private int _guitarNoteIndex;
        private int _bassNoteIndex;
        private int _keysNoteIndex;
        private int _proKeysNoteIndex;
        private int _drumNoteIndex;
        private int _vocalNoteIndex;

        private int _guitarAnimationIndex;
        private int _bassAnimationIndex;
        private int _drumAnimationIndex;

        private int _guitarTriggerIndex;
        private int _bassTriggerIndex;
        private int _drumTriggerIndex;
        private int _keysTriggerIndex;
        private int _proKeysTriggerIndex;
        private int _vocalTriggerIndex;

        private List<TempoChange> _tempoList;
        private int               _tempoIndex;
        private int               _previousTempoIndex;
        private TempoChange       _currentTempo;

        private double                 _hatTimer;

        // Text event animation triggers from the individual instrumentdifficulty tracks
        // TODO: Parse these in the parser and merge them with the AnimationEvent stuff
        private List<AnimationTrigger> _guitarMaps;
        private List<AnimationTrigger> _bassMaps;
        private List<AnimationTrigger> _drumMaps;
        private List<AnimationTrigger> _vocalMaps;
        private List<AnimationTrigger> _keysMaps;

        private bool _songHasDrumAnimations;

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

            InstrumentTrack<GuitarNote> guitarTrack = chart.GetFiveFretTrack(Instrument.FiveFretGuitar);
            InstrumentTrack<GuitarNote> bassTrack = chart.GetFiveFretTrack(Instrument.FiveFretBass);;
            InstrumentTrack<DrumNote> drumsTrack = chart.GetDrumsTrack(Instrument.ProDrums);

            _guitarNotes = guitarId.Notes;
            _bassNotes = bassId.Notes;
            _keysNotes = keysId.Notes;
            _proKeysNotes = proKeysId.Notes;
            _vocalNotes = vocalsId.NotePhrases;
            _drumNotes = drumsId.Notes;

            _guitarAnimationEvents = guitarTrack.AnimationEvents;
            _bassAnimationEvents = bassTrack.AnimationEvents;
            _drumAnimationEvents = drumsTrack.AnimationEvents;

            // This will eventually be combined into the animation events stuff, but for now the text events from the
            // individual instrument difficulties are separate
            _guitarMaps = ParseTextEvents(guitarId.TextEvents);
            _bassMaps = ParseTextEvents(bassId.TextEvents);
            _drumMaps = ParseTextEvents(drumsId.TextEvents);
            _vocalMaps = ParseTextEvents(vocalsId.TextEvents);
            _keysMaps = ParseTextEvents(keysId.TextEvents);

            _tempoList = chart.SyncTrack.Tempos;

            if (_drumAnimationEvents.Count > 0)
            {
                _songHasDrumAnimations = true;
                // Find the drummer and tell it that there are animations
                foreach (var key in _characters.Keys)
                {
                    if (_characters[key].Type == VenueCharacter.CharacterType.Drums)
                    {
                        _characters[key].ChartHasAnimations = true;
                    }
                }
            }

            // If we have at least [idle] and [playing] set ChartHasAnimations for the character
            if (_guitarMaps.FindLastIndex(e => e.State == AnimationState.Idle) > 0)
            {
                foreach (var key in _characters.Keys)
                {
                    if (_characters[key].Type == VenueCharacter.CharacterType.Guitar)
                    {
                        _characters[key].ChartHasAnimations = true;
                    }
                }
            }

            if (_bassMaps.FindLastIndex(e => e.State == AnimationState.Idle) > 0)
            {
                foreach (var key in _characters.Keys)
                {
                    if (_characters[key].Type == VenueCharacter.CharacterType.Bass)
                    {
                        _characters[key].ChartHasAnimations = true;
                    }
                }
            }

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


            while (_guitarMaps.Count > 0 && _guitarTriggerIndex < _guitarMaps.Count &&
                _guitarMaps[_guitarTriggerIndex].Time - character.TimeToFirstHit <= GameManager.SongTime)
            {
                var mapEvent = _guitarMaps[_guitarTriggerIndex];
                _guitarTriggerIndex++;

                character.OnGuitarAnimation(mapEvent);
            }

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

                while (_guitarAnimationEvents.Count > 0 && _guitarAnimationIndex < _guitarAnimationEvents.Count &&
                    _guitarAnimationEvents[_guitarAnimationIndex].Time - 0.1f <= GameManager.SongTime)
                {
                    var animEvent = _guitarAnimationEvents[_guitarAnimationIndex];
                    _guitarAnimationIndex++;

                    character.OnGuitarAnimation(animEvent.Type);
                }

                character.OnNote(note);
            }
        }

        private void ProcessBass(VenueCharacter character)
        {
            while (_bassMaps.Count > 0 && _bassTriggerIndex < _bassMaps.Count &&
                _bassMaps[_bassTriggerIndex].Time - character.TimeToFirstHit <= GameManager.SongTime)
            {
                var mapEvent = _bassMaps[_bassTriggerIndex];
                _bassTriggerIndex++;

                character.OnGuitarAnimation(mapEvent);
            }

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


                // Notify the character
                character.OnNote(note);
            }

            while (_bassAnimationEvents.Count > 0 && _bassAnimationIndex < _bassAnimationEvents.Count &&
                _bassAnimationEvents[_bassAnimationIndex].Time - 0.1f <= GameManager.SongTime) // The -0.1f is on the assumption that the action is supposed to complete at the note on time
            {
                var animEvent = _bassAnimationEvents[_bassAnimationIndex];
                _bassAnimationIndex++;

                character.OnGuitarAnimation(animEvent.Type);
            }
        }

        // TODO: Figure out something reasonable to do for the vocalist
        private void ProcessVocals(VenueCharacter character)
        {

        }

        private void ProcessDrums(VenueCharacter character)
        {
            // Deal with the fact that we have to close the hat when the open hat note ends
            if (_hatTimer > 0)
            {
                _hatTimer -= Time.deltaTime;
                if (_hatTimer <= 0)
                {
                    _hatTimer = 0;
                    character.OnDrumAnimation(AnimationEvent.AnimationType.CloseHiHat);
                }
            }

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

                if (!_songHasDrumAnimations)
                {
                    character.OnNote(note);
                    return;
                }

                while (_drumAnimationEvents.Count > 0 && _drumAnimationIndex < _drumAnimationEvents.Count &&
                    _drumAnimationEvents[_drumAnimationIndex].Time - character.TimeToFirstHit <= GameManager.SongTime)
                {
                    var animEvent = _drumAnimationEvents[_drumAnimationIndex];
                    _drumAnimationIndex++;

                    character.OnDrumAnimation(animEvent.Type);

                    if (animEvent.Type == AnimationEvent.AnimationType.OpenHiHat)
                    {
                        _hatTimer = animEvent.TimeLength;
                    }
                }
            }
        }

        // TODO: Move this into the chart parser, this is just an expediency for testing
        private List<AnimationTrigger> ParseTextEvents(List<TextEvent> events)
        {
            // Check that the trigger track is initialized
            var animationTriggerTrack = new List<AnimationTrigger>();

            // This is ugly as shit, but see the note above
            foreach (var textEvent in events)
            {
                if (textEvent.Text == "idle")
                {
                    animationTriggerTrack.Add(new AnimationTrigger(TriggerType.AnimationState, AnimationState.Idle, default, default, textEvent.Time));
                }
                else if (textEvent.Text == "idle_realtime")
                {
                    animationTriggerTrack.Add(new AnimationTrigger(TriggerType.AnimationState, AnimationState.IdleRealtime, default, default, textEvent.Time));
                }
                else if (textEvent.Text == "idle_intense")
                {
                    animationTriggerTrack.Add(new AnimationTrigger(TriggerType.AnimationState, AnimationState.IdleIntense, default, default, textEvent.Time));
                }
                else if (textEvent.Text == "play")
                {
                    animationTriggerTrack.Add(new AnimationTrigger(TriggerType.AnimationState, AnimationState.Play, default, default, textEvent.Time));
                }
                else if (textEvent.Text == "play_solo")
                {
                    animationTriggerTrack.Add(new AnimationTrigger(TriggerType.AnimationState, AnimationState.PlaySolo, default, default, textEvent.Time));
                }
                else if (textEvent.Text == "mellow")
                {
                    animationTriggerTrack.Add(new AnimationTrigger(TriggerType.AnimationState, AnimationState.Mellow, default, default, textEvent.Time));
                }
                else if (textEvent.Text == "intense")
                {
                    animationTriggerTrack.Add(new AnimationTrigger(TriggerType.AnimationState, AnimationState.Intense, default, default, textEvent.Time));
                }
                else if (textEvent.Text.StartsWith("map "))
                {
                    var parts = textEvent.Text.Split(' ');
                    if (parts.Length < 2)
                    {
                        continue;
                    }

                    // Throw everything after the _ into the mapType variable..we don't care about the HandMap_ part
                    var mapString = parts[1];
                    var mapParts = parts[1].Split("_");
                    // Having an extra _ is annoying
                    var mapType = string.Join("_", mapParts[1..]);

                    if (mapParts[0] == "HandMap")
                    {
                        switch (mapType)
                        {
                            case "Default":
                                animationTriggerTrack.Add(new AnimationTrigger(TriggerType.HandMap, default,
                                    HandMap.HandMapDefault, default, textEvent.Time));
                                break;
                            case "NoChords":
                                animationTriggerTrack.Add(new AnimationTrigger(TriggerType.HandMap, default,
                                    HandMap.HandMapNoChords, default, textEvent.Time));
                                break;
                            case "AllChords":
                                animationTriggerTrack.Add(new AnimationTrigger(TriggerType.HandMap, default,
                                    HandMap.HandMapAllChords, default, textEvent.Time));
                                break;
                            case "AllBend":
                                animationTriggerTrack.Add(new AnimationTrigger(TriggerType.HandMap, default,
                                    HandMap.HandMapAllBend, default, textEvent.Time));
                                break;
                            case "Solo":
                                animationTriggerTrack.Add(new AnimationTrigger(TriggerType.HandMap, default,
                                    HandMap.HandMapSolo, default, textEvent.Time));
                                break;
                            case "DropD":
                                animationTriggerTrack.Add(new AnimationTrigger(TriggerType.HandMap, default,
                                    HandMap.HandMapDropD, default, textEvent.Time));
                                break;
                            case "DropD2":
                                animationTriggerTrack.Add(new AnimationTrigger(TriggerType.HandMap, default,
                                    HandMap.HandMapDropD2, default, textEvent.Time));
                                break;
                            case "Chord_C":
                                animationTriggerTrack.Add(new AnimationTrigger(TriggerType.HandMap, default,
                                    HandMap.HandMapChordC, default, textEvent.Time));
                                break;
                            case "Chord_D":
                                animationTriggerTrack.Add(new AnimationTrigger(TriggerType.HandMap, default,
                                    HandMap.HandMapChordD, default, textEvent.Time));
                                break;
                            case "Chord_A":
                                animationTriggerTrack.Add(new AnimationTrigger(TriggerType.HandMap, default,
                                    HandMap.HandMapChordA, default, textEvent.Time));
                                break;
                        }
                    }
                    else if (mapParts[0] == "StrumMap")
                    {
                        switch (mapType)
                        {
                            case "Default":
                                animationTriggerTrack.Add(new AnimationTrigger(TriggerType.StrumMap, default, default, StrumMap.StrumMapDefault, textEvent.Time));
                                break;
                            case "Pick":
                                animationTriggerTrack.Add(new AnimationTrigger(TriggerType.StrumMap, default, default, StrumMap.StrumMapPick, textEvent.Time));
                                break;
                            case "SlapBass":
                                animationTriggerTrack.Add(new AnimationTrigger(TriggerType.StrumMap, default, default, StrumMap.StrumMapSlapBass, textEvent.Time));
                                break;
                        }
                    }
                }
            }

            return animationTriggerTrack;
        }

        public enum AnimationState
        {
            Idle,
            IdleRealtime,
            IdleIntense,
            Play,
            PlaySolo,
            Mellow,
            Intense
        }

        public enum HandMap
        {
            HandMapDefault,
            HandMapNoChords,
            HandMapAllChords,
            HandMapAllBend,
            HandMapSolo,
            HandMapDropD,
            HandMapDropD2,
            HandMapChordC,
            HandMapChordD,
            HandMapChordA,
        }

        public enum StrumMap {
            StrumMapDefault,
            StrumMapPick,
            StrumMapSlapBass
        }

        public enum TriggerType
        {
            AnimationState,
            HandMap,
            StrumMap
        }

        public struct AnimationTrigger
        {
            public TriggerType Type;
            public AnimationState State;
            public HandMap HandMap;
            public StrumMap StrumMap;
            public double Time;

            public AnimationTrigger(TriggerType type, AnimationState state, HandMap handMap, StrumMap strumMap, double time)
            {
                Type = type;
                State = state;
                HandMap = handMap;
                StrumMap = strumMap;
                Time = time;
            }
        }
    }
}
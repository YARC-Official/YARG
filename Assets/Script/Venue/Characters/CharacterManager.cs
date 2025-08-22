using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Chart.Events;
using YARG.Gameplay;
using AnimationEvent = YARG.Core.Chart.AnimationEvent;
using CharacterStateType = YARG.Core.Chart.Events.CharacterState.CharacterStateType;
using HandMapType = YARG.Core.Chart.Events.HandMap.HandMapType;
using StrumMapType = YARG.Core.Chart.Events.StrumMap.StrumMapType;

namespace YARG.Venue.Characters
{
    public class CharacterManager : GameplayBehaviour
    {

        [SerializeField]
        private GameObject _venue;

        private readonly Dictionary<VenueCharacter.CharacterType, VenueCharacter> _characters = new();

        // Ugh, the different note types ruin me again
        private List<VocalsPhrase> _vocalNotes;
        private List<DrumNote>     _drumNotes;
        private List<GuitarNote>   _guitarNotes;
        private List<GuitarNote>   _bassNotes;
        private List<GuitarNote>   _keysNotes;
        private List<ProKeysNote>  _proKeysNotes;

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
        private List<AnimationTrigger> _guitarMaps;
        private List<AnimationTrigger> _bassMaps;
        private List<AnimationTrigger> _drumMaps;
        private List<AnimationTrigger> _vocalMaps;
        private List<AnimationTrigger> _keysMaps;
        private List<AnimationTrigger> _proKeysMaps;

        private bool _songHasDrumAnimations;

        protected override void OnChartLoaded(SongChart chart)
        {
            // Find all the VenueCharacters in the venue

            var venueCharacters = _venue.GetComponentsInChildren<VenueCharacter>(true);

            foreach (var character in venueCharacters)
            {
                _characters.Add(character.Type, character);
            }

            // Get the expert notes for each track
            // TODO: This should get the highest available difficulty, in case Expert doesn't exist
            var guitarId = chart.FiveFretGuitar.GetDifficulty(Difficulty.Expert);
            var bassId = chart.FiveFretBass.GetDifficulty(Difficulty.Expert);
            var keysId = chart.Keys.GetDifficulty(Difficulty.Expert);
            var proKeysId = chart.ProKeys.GetDifficulty(Difficulty.Expert);
            var vocalsId = chart.Vocals.Parts[0];
            var drumsId = chart.ProDrums.GetDifficulty(Difficulty.Expert);

            InstrumentTrack<GuitarNote> guitarTrack = chart.GetFiveFretTrack(Instrument.FiveFretGuitar);
            InstrumentTrack<GuitarNote> bassTrack = chart.GetFiveFretTrack(Instrument.FiveFretBass);
            InstrumentTrack<DrumNote> drumsTrack = chart.GetDrumsTrack(Instrument.ProDrums);
            VocalsTrack vocalsTrack = chart.GetVocalsTrack(Instrument.Vocals);
            InstrumentTrack<GuitarNote> keysTrack = chart.GetFiveFretTrack(Instrument.Keys);
            InstrumentTrack<ProKeysNote> proKeysTrack = chart.ProKeys;

            _guitarNotes = guitarId.Notes;
            _bassNotes = bassId.Notes;
            _keysNotes = keysId.Notes;
            _proKeysNotes = proKeysId.Notes;
            _vocalNotes = vocalsId.NotePhrases;
            _drumNotes = drumsId.Notes;

            _guitarAnimationEvents = guitarTrack.Animations.AnimationEvents;
            _bassAnimationEvents = bassTrack.Animations.AnimationEvents;
            _drumAnimationEvents = drumsTrack.Animations.AnimationEvents;

            // This will eventually be combined into the animation events stuff, but for now the text events from the
            // individual instrument difficulties are separate
            _guitarMaps = ProcessAnimationEvents(guitarTrack);
            _bassMaps = ProcessAnimationEvents(bassTrack);
            _drumMaps = ProcessAnimationEvents(drumsTrack);
            _vocalMaps = ProcessAnimationEvents(vocalsTrack);
            _keysMaps = ProcessAnimationEvents(keysTrack);
            _proKeysMaps = ProcessAnimationEvents(proKeysTrack);

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
            if (_guitarMaps.Count > 0)
            {
                foreach (var key in _characters.Keys)
                {
                    if (_characters[key].Type == VenueCharacter.CharacterType.Guitar)
                    {
                        _characters[key].ChartHasAnimations = true;
                    }
                }
            }

            if (_bassMaps.Count > 0)
            {
                foreach (var key in _characters.Keys)
                {
                    if (_characters[key].Type == VenueCharacter.CharacterType.Bass)
                    {
                        _characters[key].ChartHasAnimations = true;
                    }
                }
            }

            if (_drumMaps.Count > 0)
            {
                foreach (var key in _characters.Keys)
                {
                    if (_characters[key].Type == VenueCharacter.CharacterType.Drums)
                    {
                        _characters[key].ChartHasAnimations = true;
                    }
                }
            }

            if (_vocalMaps.Count > 0)
            {
                foreach (var key in _characters.Keys)
                {
                    if (_characters[key].Type == VenueCharacter.CharacterType.Vocals)
                    {
                        _characters[key].ChartHasAnimations = true;
                    }
                }
            }

            // Don't animate until there are notes (at least until we have idle animations)
            // foreach (var character in _characters.Values)
            // {
            //     character.StopAnimation();
            // }
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
            while (_vocalMaps.Count > 0 && _vocalTriggerIndex < _vocalMaps.Count &&
                _vocalMaps[_vocalTriggerIndex].Time - character.TimeToFirstHit <= GameManager.SongTime)
            {
                var mapEvent = _vocalMaps[_vocalTriggerIndex];
                _vocalTriggerIndex++;

                character.OnGuitarAnimation(mapEvent);
            }
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

        // Translate chart events into animation triggers
        private static List<AnimationTrigger> ProcessAnimationEvents<T>(InstrumentTrack<T> track) where T : Note<T>
        {
            var handMaps = track.Animations.HandMaps;
            var strumMaps = track.Animations.StrumMaps;
            var states = track.Animations.CharacterStates;

            return ProcessAnimationEvents(handMaps, strumMaps, states);
        }

        // Vocals has to be different, of course
        private static List<AnimationTrigger> ProcessAnimationEvents(VocalsTrack track)
        {
            // Yes, only states is actually valid here, but we may as well use the existing empty lists rather than
            // making new ones
            var handMaps = track.Animations.HandMaps;
            var strumMaps = track.Animations.StrumMaps;
            var states = track.Animations.CharacterStates;

            return ProcessAnimationEvents(handMaps, strumMaps, states);
        }

        private static List<AnimationTrigger> ProcessAnimationEvents(List<HandMap> handMaps, List<StrumMap> strumMaps,
            List<CharacterState> states)
        {
            var triggers = new List<AnimationTrigger>();

            // Deal with the hand maps
            foreach (var handMap in handMaps)
            {
                triggers.Add(new AnimationTrigger(TriggerType.HandMap, default, handMap.Type, default, handMap.Time));
            }

            foreach (var strumMap in strumMaps)
            {
                triggers.Add(new AnimationTrigger(TriggerType.StrumMap, default, default, strumMap.Type, strumMap.Time));
            }

            foreach (var state in states)
            {
                triggers.Add(new AnimationTrigger(TriggerType.AnimationState, state.Type, default, default, state.Time));
            }

            // Make sure they are in the correct time order since we just jammed together a bunch of different lists
            triggers.Sort((a, b) => a.Time.CompareTo(b.Time));

            return triggers;
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
            public CharacterStateType State;
            public HandMapType HandMap;
            public StrumMapType StrumMap;
            public double Time;

            public AnimationTrigger(TriggerType type, CharacterStateType state, HandMapType handMap, StrumMapType strumMap, double time)
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
using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Logging;
using AnimationTrigger = YARG.Venue.Characters.CharacterManager.AnimationTrigger;
using CharacterStateType = YARG.Core.Chart.Events.CharacterState.CharacterStateType;
using AnimationType = YARG.Core.Chart.AnimationEvent.AnimationType;
using HandMapType = YARG.Core.Chart.Events.HandMap.HandMapType;
using StrumMapType = YARG.Core.Chart.Events.StrumMap.StrumMapType;

#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

namespace YARG.Venue.Characters
{
    [RequireComponent(typeof(Animator))]
    public partial class VenueCharacter : MonoBehaviour
    {
        public enum CharacterType
        {
            Bass,
            Guitar,
            Drums,
            Vocals,
            Keys,
        }

        [SerializeField]
        private CharacterManager _characterManager;

        [SerializeField]
        public CharacterType Type;

        [SerializeField]
        private int _actionsPerAnimationCycle;

        [Space]

        [SerializeField]
        private bool _enableAnimationStates;
        [SerializeField]
        private string _idleAnimationName;
        [SerializeField]
        private string _playingAnimationName;
        [SerializeField]
        private int _framesToFirstHit;
        [Space]
        [SerializeField]
        private List<string> _strumUpStates = new();
        [SerializeField]
        [HideInInspector]
        public AnimationDictionary LayerStates;

        [SerializeField]
        private AnimationStateMap _animationStates;


        private Dictionary<string, List<string>> _layerStates;

        private bool _ikActive;
        private Transform _leftHandObject;

        private HashSet<string> _availableLayers     = new();
        private HashSet<string> _availableParameters = new();

        private int _idleAnimationHash;
        private int _playingAnimationHash;
        private int _kickAnimationHash;

        private RuntimeAnimatorController _animatorController;
        private Animator _animator;

        private float _animationLength;
        private float _animationSpeed;
        private int   _speedAdjustmentHash;
        private int   _unclampedSpeedAdjustmentHash;

        private bool _isAnimating;

        private string _currentLeftHandPosition;

        private List<int> _strumUpHashes = new();

        #nullable enable
        private Dictionary<string, Transform?> _ikTargets = new();
        #nullable disable

        [NonSerialized]
        public float TimeToFirstHit = 0.0f;

        private AnimationState _animationState;
        private HandMapType    _handMap;
        private StrumMapType   _strumMap;

        private bool _inhibitHandShape;
        private bool _isBend;
        private bool _handDown;

        private AnimationStateType? _delayedTriggerState;
        private float               _delayedTriggerTime;

        private bool _alwaysBend => _handMap == HandMapType.AllBend;


        private bool _hasAdvancedAnimations;
        private bool _hasSlap;
        private bool _hasPick;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _animatorController = _animator.runtimeAnimatorController;

            _layerStates = LayerStates.ToDictionary();

            PopulateAnimationData();
            CheckAdvancedAnimations();

            // Get the available animations so we don't try to call ones the venue author didn't implement
            foreach (var animation in _animatorController.animationClips)
            {
                _availableAnimations.Add(animation.name);
            }

            foreach (var state in _strumUpStates)
            {
                _strumUpHashes.Add(Animator.StringToHash(state));
            }

            var clip = _animatorController.animationClips[0];
            _animationLength = clip.length;
            TimeToFirstHit = _framesToFirstHit / clip.frameRate;

            _speedAdjustmentHash = Animator.StringToHash("SpeedAdjustment");
            _unclampedSpeedAdjustmentHash = Animator.StringToHash("UnclampedSpeedAdjustment");

            if (_enableAnimationStates) {
                _idleAnimationHash = Animator.StringToHash(_idleAnimationName);
                _playingAnimationHash = Animator.StringToHash(_playingAnimationName);
            }

            GetPositionHashes();
            GetIKTargets();

        }

        private void CheckAdvancedAnimations()
        {
            bool allFound = true;

            string[] requiredGuitar =
            {
                "StrumUp",
                "StrumDown",
                "HandPositionOne",
                "HandPositionTwo",
                "HandPositionThree",
                "HandPositionFour",
                "HandPositionFive",
                "HandPositionSix",
                "HandPositionSeven",
                "HandPositionEight"
            };

            // For guitar/bass, we require strumup/strumdown and some hand positions
            // TODO: Make this check for animations and not just states
            if (Type == CharacterType.Guitar || Type == CharacterType.Bass)
            {
                foreach (var name in requiredGuitar)
                {
                    if (!_animationEvents.TryGet(name, out var info))
                    {
                        allFound = false;
                        break;
                    }
                }

                _hasAdvancedAnimations = allFound;
                return;
            }

            string[] requiredDrums =
            {
                "Kick",
                "HihatLeft",
                "HihatRight",
                "Tom1Left",
                "Tom1Right",
                "Tom2Left",
                "Tom2Right",
                "FloorTomLeft",
                "FloorTomRight",
                "RideLeft",
                "RideRight",
            };

            // For drums, we require the full suite, minus soft/hard variants (but we don't actually check the ones with variants)
            if (Type == CharacterType.Drums)
            {
                foreach (var name in requiredDrums)
                {
                    if (!_animationEvents.TryGet(name, out var info))
                    {
                        allFound = false;
                        break;
                    }
                }
                _hasAdvancedAnimations = allFound;
                return;
            }

            // TODO: Add check for vocals/keys when we actually support those

            _hasAdvancedAnimations = false;
        }

        private void Update()
        {
            if (_delayedTriggerTime > 0)
            {
                _delayedTriggerTime -= Time.deltaTime;
                if (_delayedTriggerTime <= 0 && _delayedTriggerState.HasValue)
                {
                    SetTrigger(_delayedTriggerState.Value);
                    _delayedTriggerTime = 0f;
                    _delayedTriggerState = null;
                }
            }
        }

        private void GetIKTargets()
        {
            switch (Type)
            {
                case CharacterType.Guitar:
                    GetGuitarIKTargets();
                    break;
                case CharacterType.Drums:
                    GetDrumsIKTargets();
                    break;
            }
        }

        private void GetDrumsIKTargets()
        {

        }

        private void GetGuitarIKTargets()
        {
            string[] positions =
            {
                "HandPositionOne",
                "HandPositionTwo",
                "HandPositionThree",
                "HandPositionFour",
                "HandPositionFive",
                "HandPositionSix",
                "HandPositionSeven",
                "HandPositionEight",
                "HandPositionNine",
                "HandPositionTen",
                "HandPositionEleven",
                "HandPositionTwelve",
                "HandPositionThirteen",
                "HandPositionFourteen",
                "HandPositionFifteen",
                "HandPositionSixteen"
            };

            foreach (var transform in GetComponentsInChildren<Transform>())
            {
                if (transform.name.Contains("HandPosition"))
                {
                    _ikTargets.Add(transform.name, transform);
                }
            }

            if (_ikTargets.Count > 0)
            {
                _ikActive = true;
            }
        }

        private void GetPositionHashes()
        {
            string[] positions =
            {
                "HandPositionOne",
                "HandPositionTwo",
                "HandPositionThree",
                "HandPositionFour",
                "HandPositionFive",
                "HandPositionSix",
                "HandPositionSeven",
                "HandPositionEight",
                "HandPositionNine",
                "HandPositionTen",
                "HandPositionEleven",
                "HandPositionTwelve",
                "HandPositionThirteen",
                "HandPositionFourteen",
                "HandPositionFifteen",
                "HandPositionSixteen"
            };

            foreach (var position in positions)
            {
                _leftHandPositionHashes.Add(position, Animator.StringToHash(position));
            }
        }

        private void HandleStrumMap(StrumMapType strumMap)
        {
            // Strum map is only valid for bass
            if (Type != CharacterType.Bass)
            {
                return;
            }

            _strumMap = strumMap;

            YargLogger.LogDebug($"Strum map {strumMap} set");
        }

        private void HandleCharacterState(CharacterStateType characterState)
        {
            SetTrigger(characterState);
        }

        private void HandleHandMap(HandMapType handMap)
        {
            // Hand map is only valid for guitar and bass
            if (Type != CharacterType.Guitar)
            {
                return;
            }

            if (handMap != _handMap)
            {
                _inhibitHandShape = false;
                _handMap = handMap;
                var trigger = GetAnimationStateForHandMap(handMap);
                // If this is a forced chord shape, we have to set the inhibit flag
                switch (handMap)
                {
                    case HandMapType.ChordA:
                    case HandMapType.ChordC:
                    case HandMapType.ChordD:
                    case HandMapType.DropD:
                    case HandMapType.DropD2:
                        _inhibitHandShape = true;
                        break;
                }

                if (trigger.HasValue)
                {
                    SetTrigger(trigger.Value);
                }

                if (handMap == HandMapType.Default)
                {
                    YargLogger.LogDebug("Default hand shape triggered");
                }
            }
        }

        public void OnGuitarAnimation(AnimationTrigger animation)
        {
            switch (animation.Type)
            {
                case CharacterManager.TriggerType.AnimationState:
                    HandleCharacterState(animation.State);
                    break;
                case CharacterManager.TriggerType.HandMap:
                    HandleHandMap(animation.HandMap);
                    break;
                case CharacterManager.TriggerType.StrumMap:
                    HandleStrumMap(animation.StrumMap);
                    break;
            }
        }

        public void OnGuitarAnimation(AnimationType animation)
        {
            if (_animationEvents.TryGet(animation, out var animInfo))
            {
                // YargLogger.LogDebug($"Animation {animInfo} triggered");
                SetTrigger(animInfo);
                return;
            }

            YargLogger.LogDebug($"Animation {animation} not found for character type {Type}");
        }

        public void OnDrumAnimation(AnimationType animation)
        {
            AnimationStateType? animState = animation switch
            {
                AnimationType.Kick => AnimationStateType.Kick,
                AnimationType.OpenHiHat => AnimationStateType.OpenHiHat,
                AnimationType.CloseHiHat => AnimationStateType.CloseHiHat,
                AnimationType.HihatLeftHand => AnimationStateType.HihatLeftHand,
                AnimationType.HihatRightHand => AnimationStateType.HihatRightHand,
                AnimationType.SnareLhHard => AnimationStateType.SnareLhHard,
                AnimationType.SnareRhHard => AnimationStateType.SnareRhHard,
                AnimationType.SnareLhSoft => AnimationStateType.SnareLhSoft,
                AnimationType.SnareRhSoft => AnimationStateType.SnareRhSoft,
                AnimationType.Crash1LhHard => AnimationStateType.Crash1LhHard,
                AnimationType.Crash1LhSoft => AnimationStateType.Crash1LhSoft,
                AnimationType.Crash1RhHard => AnimationStateType.Crash1RhHard,
                AnimationType.Crash1RhSoft => AnimationStateType.Crash1RhSoft,
                AnimationType.Crash2LhHard => AnimationStateType.Crash2LhHard,
                AnimationType.Crash2LhSoft => AnimationStateType.Crash2LhSoft,
                AnimationType.Crash2RhHard => AnimationStateType.Crash2RhHard,
                AnimationType.Crash2RhSoft => AnimationStateType.Crash2RhSoft,
                AnimationType.RideLh => AnimationStateType.RideLh,
                AnimationType.RideRh => AnimationStateType.RideRh,
                AnimationType.Tom1LeftHand => AnimationStateType.Tom1LeftHand,
                AnimationType.Tom1RightHand => AnimationStateType.Tom1RightHand,
                AnimationType.Tom2LeftHand => AnimationStateType.Tom2LeftHand,
                AnimationType.Tom2RightHand => AnimationStateType.Tom2RightHand,
                AnimationType.FloorTomLeftHand => AnimationStateType.FloorTomLeftHand,
                AnimationType.FloorTomRightHand => AnimationStateType.FloorTomRightHand,
                _ => null
            };

            if (animState.HasValue)
            {
                SetTrigger(animState.Value);
            }

        }

        public void OnNote<T>(Note<T> note) where T : Note<T>
        {

            if (note is GuitarNote gNote)
            {
                // Handle alternate strums for bass
                if (Type == CharacterType.Bass && _hasSlap && _strumMap == StrumMapType.SlapBass)
                {
                    // Just trigger slap and return
                    SetTrigger(AnimationStateType.Slap);
                    return;
                }

                // Which layer has strum down?
                if (!_animationEvents.TryGet(AnimationStateType.StrumDown, out var strumDownEvent))
                {
                    // TODO: Fall back to basic animations in this case
                    return;
                }
                // Figure out which way to strum...I guess by looking at which state we're currently in?
                // TODO: Handle the case where the state exists in more than one layer
                var currentState = _animator.GetCurrentAnimatorStateInfo(strumDownEvent[0].Layer);

                bool strumUp = false;
                if (!_animationEvents.TryGet(currentState.shortNameHash, out var currentStateInfo))
                {
                    foreach (var hash in _strumUpHashes)
                    {
                        if (currentState.shortNameHash == hash)
                        {
                            strumUp = true;
                            break;
                        }
                    }
                }
                else if (currentStateInfo.Type == AnimationStateType.StrumDown)
                {
                    strumUp = true;
                }

                if (strumUp)
                {
                    SetTrigger(AnimationStateType.StrumUp);
                }
                else
                {
                    SetTrigger(AnimationStateType.StrumDown);
                }

                SetHandAnimationForNote(gNote);
            }

            if (note is Note<VocalNote>)
            {

            }

            if (note is Note<ProKeysNote>)
            {

            }
        }

        private void SetHandAnimationForNote(GuitarNote gNote)
        {
            int lowestFret = 5;
            bool openGreen = _handMap is HandMapType.DropD or HandMapType.DropD2;
            bool useChordShape =
                (gNote.IsChord && (!_inhibitHandShape || Type != CharacterType.Guitar) &&
                    _handMap != HandMapType.NoChords) || _handMap == HandMapType.AllChords;
            bool isSustain = gNote.IsSustain;
            float sustainLength = (float) gNote.TimeLength;

            if (_inhibitHandShape && Type == CharacterType.Guitar && (_handMap != HandMapType.DropD && _handMap != HandMapType.DropD2))
            {
                return;
            }

            // TODO: Get the length of each animation rather than hardcoding the sustainLength value
            if (_alwaysBend)
            {
                sustainLength = 0.333f;
            }

            // Just for testing
            if (Type != CharacterType.Guitar)
            {
                return;
            }

            // Cancel any delayed trigger since we're either changing hand shapes or just received a new note
            CancelDelayedTrigger();

            // Shift hand positions based on whether the note is a chord or not and the position of the lowest note
            if (useChordShape)
            {
                // Find the note with the lowest fret, and if any are sustains, consider this a sustain chord
                foreach (var child in gNote.AllNotes)
                {
                    if (child.IsSustain)
                    {
                        isSustain = true;
                        sustainLength = Math.Max(sustainLength, (float) child.TimeLength);
                    }
                    if (child.Fret < lowestFret)
                    {
                        lowestFret = child.Fret;
                    }
                }

                if (HandleDropD(isSustain, sustainLength))
                {
                    return;
                }

                if (lowestFret < 3)
                {
                    if (isSustain || _alwaysBend)
                    {
                        if (_currentChordShape == ChordShape.ChordLowVibrato)
                        {
                            return;
                        }

                        // Trigger chord low vibrato hand shape
                        SetTrigger(AnimationStateType.LhChordLowVibrato);
                        SetDelayedTrigger(AnimationStateType.LhChordLow, sustainLength);
                    }
                    else
                    {
                        if (_currentChordShape == ChordShape.ChordLow)
                        {
                            return;
                        }

                        // Trigger chord low hand shape
                        SetTrigger(AnimationStateType.LhChordLow);
                        _currentChordShape = ChordShape.ChordLow;
                    }
                }
                else
                {
                    if (isSustain || _alwaysBend)
                    {
                        if (_currentChordShape == ChordShape.ChordHighVibrato)
                        {
                            return;
                        }

                        // Trigger chord high vibrato hand shape
                        SetTrigger(AnimationStateType.LhChordHighVibrato);
                        _currentChordShape = ChordShape.ChordHighVibrato;
                        SetDelayedTrigger(AnimationStateType.LhChordHigh, sustainLength);
                    }
                    else
                    {
                        if (_currentChordShape == ChordShape.ChordHigh)
                        {
                            return;
                        }

                        // Trigger chord high hand shape
                        SetTrigger(AnimationStateType.LhChordHigh);
                        _currentChordShape = ChordShape.ChordHigh;
                    }
                }
            }
            else if (gNote.Fret != (int)FiveFretGuitarFret.Open && !(openGreen && gNote.Fret == (int)FiveFretGuitarFret.Green))
            {
                if (HandleDropD(isSustain, sustainLength))
                {
                    return;
                }

                if (gNote.Fret < 3)
                {
                    if (isSustain || _alwaysBend)
                    {
                        if (_currentChordShape == ChordShape.ChordLowVibrato)
                        {
                            return;
                        }

                        // Trigger single note low vibrato hand shape
                        SetTrigger(AnimationStateType.LhSingleLowVibrato);
                        _currentChordShape = ChordShape.ChordLowVibrato;
                        SetDelayedTrigger(AnimationStateType.LhSingleLow, sustainLength);
                    }
                    else
                    {
                        if (_currentChordShape == ChordShape.SingleLow)
                        {
                            return;
                        }

                        // Trigger single note low hand shape
                        SetTrigger(AnimationStateType.LhSingleLow);
                        _currentChordShape = ChordShape.SingleLow;
                    }
                }
                else
                {
                    if (isSustain || _alwaysBend)
                    {
                        if (_currentChordShape == ChordShape.SingleHighVibrato)
                        {
                            return;
                        }

                        // Trigger single note high vibrato hand shape
                        SetTrigger(AnimationStateType.LhSingleHighVibrato);
                        _currentChordShape = ChordShape.SingleHighVibrato;
                        SetDelayedTrigger(AnimationStateType.LhSingleHigh, sustainLength);
                    }
                    else
                    {


                        if (_currentChordShape == ChordShape.SingleHigh)
                        {
                            return;
                        }

                        // Trigger single note high hand shape
                        SetTrigger(AnimationStateType.LhSingleHigh);
                        _currentChordShape = ChordShape.SingleHigh;
                    }
                }
            }
            else
            {
                // We either have an open note or we have green and are using a map that uses open fingering for green
                // HandleDropD(isSustain, sustainLength);

                if (_handMap == HandMapType.DropD || _handMap == HandMapType.DropD2)
                {
                    if (_currentChordShape == ChordShape.DropDOpen)
                    {
                        return;
                    }

                    SetTrigger(AnimationStateType.LhDropDOpen);
                    _currentChordShape = ChordShape.DropDOpen;
                    return;
                }

                if (_currentChordShape == ChordShape.OpenChord)
                {
                    return;
                }
                // Trigger open hand shape
                SetTrigger(AnimationStateType.LhOpenChord);
                _currentChordShape = ChordShape.OpenChord;
            }
        }

        private bool HandleDropD(bool isSustain, float sustainLength)
        {
            // TODO: Figure out what is different about DropD2 and give it different handling
            if (_handMap == HandMapType.DropD || _handMap == HandMapType.DropD2)
            {
                if (isSustain)
                {
                    if (_currentChordShape == ChordShape.DropDVibrato)
                    {
                        return true;
                    }
                    SetTrigger(AnimationStateType.LhChordDropDVibrato);
                    _currentChordShape = ChordShape.DropDVibrato;
                    SetDelayedTrigger(AnimationStateType.LhChordDropD, sustainLength);
                    return true;
                }

                if (_currentChordShape == ChordShape.DropD)
                {
                    return true;
                }

                // Trigger drop D hand shape
                SetTrigger(AnimationStateType.LhChordDropD);
                _currentChordShape = ChordShape.DropD;
                return true;
            }

            return false;
        }

        public void StopAnimation()
        {
            if (!_enableAnimationStates)
            {
                return;
            }

            _isAnimating = false;
        }

        public void StartAnimation(double secondsPerBeat)
        {
            if (!_enableAnimationStates)
            {
                return;
            }

            _isAnimating = true;

            UpdateTempo(secondsPerBeat);
        }

        public bool IsAnimating()
        {
            return _isAnimating;
        }

        public void UpdateTempo(double secondsPerBeat)
        {
            // Adjust the speed of the animation based on the song tempo
            if (_actionsPerAnimationCycle == 0)
            {
                return;
            }

            // We want one animation cycle per beat (as a multiplier, so if there are 17 actions and 0.4 seconds per beat, we want the animation to complete in 6.8 seconds)
            var secondsPerAction = _animationLength / _actionsPerAnimationCycle;
            // We want secondsPerAction to match secondsPerBeat
            float speed = (float) (secondsPerAction / secondsPerBeat);

            SetFloat(_unclampedSpeedAdjustmentHash, speed);

            // Not sure if this will work, but it's worth a try...
            if (speed >= 1.5)
            {
                speed /= 2;
            } else if (speed <= 0.6)
            {
                speed *= 2;
            }

            _animationSpeed = speed;
            SetFloat(_speedAdjustmentHash, speed);
        }

        private void OnAnimatorIK()
        {
            if (!_animator)
            {
                return;
            }

            if (!_ikTargets.TryGetValue(_currentLeftHandPosition, out _leftHandObject) || !_ikActive)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0);
                return;
            }

            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
            _animator.SetIKPosition(AvatarIKGoal.LeftHand, _leftHandObject.position);
        }

        private void SetDelayedTrigger(AnimationStateType trigger, float delay)
        {
            _delayedTriggerState = trigger;
            _delayedTriggerTime = delay;
        }

        private void CancelDelayedTrigger()
        {
            _delayedTriggerState = null;
            _delayedTriggerTime = 0;
        }

        public enum BassStrumTypes
        {
            Strum,
            Slap,
            Pick
        }
    }
}
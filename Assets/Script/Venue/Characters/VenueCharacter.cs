using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Gameplay.Visuals;
using AnimationEvent = YARG.Core.Chart.AnimationEvent;
using AnimationTrigger = YARG.Venue.Characters.CharacterManager.AnimationTrigger;
using AnimationState = YARG.Venue.Characters.CharacterManager.AnimationState;
using AnimationType = YARG.Core.Chart.AnimationEvent.AnimationType;
using HandMap = YARG.Venue.Characters.CharacterManager.HandMap;
using StrumMap = YARG.Venue.Characters.CharacterManager.StrumMap;

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

        public enum AnimationStates
        {
            Idle,
            Playing
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
        private int   _animationParamHash;

        private bool _isAnimating;

        private int _kickLayerIndex;
        private int _hatLayerIndex;
        private int _leftHandLayerIndex;
        private int _rightHandLayerIndex;

        private string _currentLeftHandPosition;

        private string _strumTrigger = "Strum";

        private List<int> _strumUpHashes = new();

        #nullable enable
        private Dictionary<string, Transform?> _ikTargets = new();
        #nullable disable

        [NonSerialized]
        public float TimeToFirstHit = 0.0f;

        private AnimationState _animationState;
        private HandMap        _handMap;
        private StrumMap       _strumMap;

        private bool _inhibitHandShape;
        private bool _isBend;
        private bool _handDown;

        private string _delayedTriggerName;
        private float  _delayedTriggerTime;

        private bool _alwaysBend => _handMap == HandMap.HandMapAllBend;
        [NonSerialized]
        public  bool ChartHasAnimations;

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

            _animationParamHash = Animator.StringToHash("SpeedAdjustment");

            if (_enableAnimationStates) {
                _idleAnimationHash = Animator.StringToHash(_idleAnimationName);
                _playingAnimationHash = Animator.StringToHash(_playingAnimationName);
            }

            if (Type == CharacterType.Drums)
            {
                // TODO: WTFBBQ, this doesn't work either
                // _kickLayerIndex = _animator.GetLayerIndex("Kick Layer");
                // _hatLayerIndex = _animator.GetLayerIndex("Hat Layer");

                _kickLayerIndex = 1;
                _hatLayerIndex = 2;
            }

            if (Type == CharacterType.Guitar || Type == CharacterType.Bass)
            {
                // TODO: Figure out why this returns -1 for Guitar
                // _leftHandLayerIndex = _animator.GetLayerIndex("Left Hand");

                // Getting the index is broke, but we know it's 1, so we'll hardcode it for now (except that it isn't for drums now..fml)
                if (Type != CharacterType.Drums)
                {
                    _leftHandLayerIndex = 1;
                    _rightHandLayerIndex = 0;
                }
                else
                {
                    _leftHandLayerIndex = 3;
                    _rightHandLayerIndex = 4;
                }
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
                if (_delayedTriggerTime <= 0 && _delayedTriggerName != null)
                {
                    _animator.SetTrigger(_delayedTriggerName);
                    _delayedTriggerTime = 0f;
                    _delayedTriggerName = null;
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
                "HandPositionEight"
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
                "HandPositionEight"
            };

            foreach (var position in positions)
            {
                _leftHandPositionHashes.Add(position, Animator.StringToHash(position));
            }
        }

        private void HandleStrumMap(StrumMap strumMap)
        {
            // Strum map is only valid for bass
            if (Type != CharacterType.Bass)
            {
                return;
            }

            _strumTrigger = strumMap switch
            {
                StrumMap.StrumMapDefault => "Strum",
                StrumMap.StrumMapPick => "Pick",
                StrumMap.StrumMapSlapBass => "Slap",
                _ => "Strum"
            };

            YargLogger.LogDebug($"Strum map {strumMap} set");
        }

        private void HandleAnimationState(AnimationState animationState)
        {
            var triggerText = animationState switch
            {
                AnimationState.Idle => "Idle",
                AnimationState.IdleIntense => "IdleIntense",
                AnimationState.IdleRealtime => "IdleRealtime",
                AnimationState.Intense => "Intense",
                AnimationState.Mellow => "Mellow",
                AnimationState.Play => "Play",
                AnimationState.PlaySolo => "PlaySolo",
                _ => "Idle"
            };

            // _animator.SetTrigger(triggerText);
        }

        private void HandleHandMap(HandMap handMap)
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
                // If this is a forced chord shape, we have to send an animation trigger and set the inhibit flag
                switch (handMap)
                {
                    case HandMap.HandMapChordA:
                        SetTrigger("ChordA");
                        _inhibitHandShape = true;
                        YargLogger.LogDebug("Chord A triggered");
                        break;
                    case HandMap.HandMapChordC:
                        SetTrigger("ChordC");
                        _inhibitHandShape = true;
                        YargLogger.LogDebug("Chord C triggered");
                        break;
                    case HandMap.HandMapChordD:
                        SetTrigger("ChordD");
                        _inhibitHandShape = true;
                        YargLogger.LogDebug("Chord D triggered");
                        break;
                    case HandMap.HandMapDropD:
                        SetTrigger("DropD");
                        _inhibitHandShape = true;
                        YargLogger.LogDebug("Drop D triggered");
                        break;
                    case HandMap.HandMapDropD2:
                        SetTrigger("DropD");
                        YargLogger.LogDebug("Drop D triggered");
                        _inhibitHandShape = true;
                        break;
                }

                if (handMap == HandMap.HandMapDefault)
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
                    HandleAnimationState(animation.State);
                    break;
                case CharacterManager.TriggerType.HandMap:
                    HandleHandMap(animation.HandMap);
                    break;
                case CharacterManager.TriggerType.StrumMap:
                    HandleStrumMap(animation.StrumMap);
                    YargLogger.LogDebug($"Strum map {animation.StrumMap} triggered");
                    _strumMap = animation.StrumMap;
                    break;
            }
        }

        public void OnGuitarAnimation(AnimationType animation)
        {
            // For testing
            if (Type == CharacterType.Bass)
            {
                return;
            }

            if (_animationEvents.TryGet(animation, out var animInfo))
            {
                SetTrigger(animInfo);
                return;
            }

            // TODO: Remove the old cruft below when it is determined that the above works correctly

            var animName = animation switch
            {
                AnimationType.LeftHandPosition1 => "HandPositionOne",
                AnimationType.LeftHandPosition2 => "HandPositionTwo",
                AnimationType.LeftHandPosition3 => "HandPositionThree",
                AnimationType.LeftHandPosition4 => "HandPositionFour",
                AnimationType.LeftHandPosition5 => "HandPositionFive",
                AnimationType.LeftHandPosition6 => "HandPositionSix",
                AnimationType.LeftHandPosition7 => "HandPositionSeven",
                AnimationType.LeftHandPosition8 => "HandPositionEight",
                AnimationType.LeftHandPosition9 => "HandPositionNine",
                AnimationType.LeftHandPosition10 => "HandPositionTen",
                AnimationType.LeftHandPosition11 => "HandPositionEleven",
                AnimationType.LeftHandPosition12 => "HandPositionTwelve",
                AnimationType.LeftHandPosition13 => "HandPositionThirteen",
                AnimationType.LeftHandPosition14 => "HandPositionFourteen",
                AnimationType.LeftHandPosition15 => "HandPositionFifteen",
                AnimationType.LeftHandPosition16 => "HandPositionSixteen",
                _ => "HandPositionSixteen" // We haven't gotten any farther yet
            };

            // YargLogger.LogDebug($"Animation {animName} triggered");

            _currentLeftHandPosition = animName;
            SetTrigger(animName);

            // _animator.CrossFadeInFixedTime(animName, 0.1f, _leftHandLayerIndex);
            // _animator.CrossFadeInFixedTime(animName, 0.1f);
            // _animator.Play(animName, _leftHandLayerIndex);
            // _animator.SetTrigger(animName);
        }

        public void OnDrumAnimation(AnimationType animation)
        {
            var animName = animation switch
            {
                AnimationType.Kick => "Kick",
                AnimationType.OpenHiHat => "OpenHat",
                AnimationType.CloseHiHat => "CloseHat",
                AnimationType.HihatLeftHand => "HihatLeft",
                AnimationType.HihatRightHand => "HihatRight",
                AnimationType.SnareLhHard => "SnareLeft",
                AnimationType.SnareRhHard => "SnareRight",
                AnimationType.SnareLhSoft => "SnareLeft",
                AnimationType.SnareRhSoft => "SnareRight",
                AnimationType.Crash1LhHard => "Crash1Left",
                AnimationType.Crash1LhSoft => "Crash1Left",
                AnimationType.Crash1RhHard => "Crash1Right",
                AnimationType.Crash1RhSoft => "Crash1Left",
                AnimationType.Crash2LhHard => "Crash2Left",
                AnimationType.Crash2LhSoft => "Crash2Left",
                AnimationType.Crash2RhHard => "Crash2Right",
                AnimationType.Crash2RhSoft => "Crash2Right",
                AnimationType.RideLh => "RideLeft",
                AnimationType.RideRh => "RideRight",
                AnimationType.Tom1LeftHand => "Tom1Left",
                AnimationType.Tom1RightHand => "Tom1Right",
                AnimationType.Tom2LeftHand => "Tom2Left",
                AnimationType.Tom2RightHand => "Tom2Right",
                AnimationType.FloorTomLeftHand => "FloorTomLeft",
                AnimationType.FloorTomRightHand => "FloorTomRight",
                _ => null
            };

            int? layerIndex = animation switch
            {
                AnimationType.Kick           => _kickLayerIndex,
                AnimationType.OpenHiHat      => _hatLayerIndex,
                AnimationType.CloseHiHat     => _hatLayerIndex,
                AnimationType.HihatLeftHand  => _leftHandLayerIndex,
                AnimationType.HihatRightHand => _rightHandLayerIndex,
                AnimationType.SnareLhHard    => _leftHandLayerIndex,
                AnimationType.SnareRhSoft    => _rightHandLayerIndex,
                AnimationType.SnareLhSoft    => _leftHandLayerIndex,
                AnimationType.SnareRhHard    => _rightHandLayerIndex,
                AnimationType.Crash1LhHard   => _leftHandLayerIndex,
                AnimationType.Crash1LhSoft   => _leftHandLayerIndex,
                AnimationType.Crash1RhHard   => _rightHandLayerIndex,
                AnimationType.Crash1RhSoft   => _rightHandLayerIndex,
                AnimationType.Crash2LhHard   => _leftHandLayerIndex,
                AnimationType.Crash2LhSoft   => _leftHandLayerIndex,
                AnimationType.Crash2RhHard   => _rightHandLayerIndex,
                AnimationType.Crash2RhSoft   => _rightHandLayerIndex,
                AnimationType.RideLh         => _leftHandLayerIndex,
                AnimationType.RideRh         => _rightHandLayerIndex,
                AnimationType.Tom1LeftHand   => _leftHandLayerIndex,
                AnimationType.Tom1RightHand  => _rightHandLayerIndex,
                AnimationType.Tom2LeftHand   => _leftHandLayerIndex,
                AnimationType.Tom2RightHand  => _rightHandLayerIndex,
                AnimationType.FloorTomLeftHand => _leftHandLayerIndex,
                AnimationType.FloorTomRightHand => _rightHandLayerIndex,
                _                                       => null
            };

            if (animName == null || !layerIndex.HasValue)
            {
                return;
            }
            //
            // if (animName == "OpenHat")
            // {
            //     YargLogger.LogDebug("OpenHat animation triggered");
            // } else if (animName == "CloseHat")
            // {
            //     YargLogger.LogDebug("CloseHat animation triggered");
            // }

            // YargLogger.LogDebug($"Animation {animName} triggered");

            // _animator.CrossFadeInFixedTime(animName, 0.1f, layerIndex.Value);
            // _animator.CrossFadeInFixedTime(animName, 0.067f);
            // _animator.CrossFade(animName, 0.1f);
            SetTrigger(animName);

        }

        public void OnNote<T>(Note<T> note) where T : Note<T>
        {

            if (note is GuitarNote gNote)
            {
                if (gNote.IsStrum)
                {
                    // Handle alternate strums for bass
                    if (Type == CharacterType.Bass && _hasSlap && _strumMap == StrumMap.StrumMapSlapBass)
                    {
                        // Just trigger slap and return
                        SetTrigger(AnimationStateType.Slap);
                        YargLogger.LogDebug("Slap animation triggered");
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
                    var nextState = _animator.GetNextAnimatorStateInfo(strumDownEvent[0].Layer);
                    var currentClip = _animator.GetCurrentAnimatorClipInfo(0)[0].clip.name;


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
                        SetTrigger("StrumUp");
                    }
                    else
                    {
                        SetTrigger("StrumDown");
                    }
                }
                SetHandAnimationForNote(gNote);
            }

            // Fake some animations if the chart doesn't have any to begin with
            if (!ChartHasAnimations && note is DrumNote dNote)
            {
                foreach (var child in dNote.AllNotes)
                {
                    var anim = GetDrumAnimationForNote(child);
                    SetTrigger(anim);
                }
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
            bool openGreen = _handMap is HandMap.HandMapDropD or HandMap.HandMapDropD2;
            // bool inhibit = _inhibitHandShape && Type == CharacterType.Guitar;
            bool useChordShape =
                (gNote.IsChord && (!_inhibitHandShape || Type != CharacterType.Guitar) &&
                    _handMap != HandMap.HandMapNoChords) || _handMap == HandMap.HandMapAllChords;
            bool isSustain = gNote.IsSustain;
            float sustainLength = (float) gNote.TimeLength;

            if (_inhibitHandShape && Type == CharacterType.Guitar && (_handMap != HandMap.HandMapDropD && _handMap != HandMap.HandMapDropD2))
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
                        if (_currentChordShape == "DefaultChordLowVibrato")
                        {
                            return;
                        }

                        // Trigger chord low vibrato hand shape
                        SetTrigger("DefaultChordLowVibrato");
                        SetDelayedTrigger("DefaultChordLow", sustainLength);
                    }
                    else
                    {
                        if (_currentChordShape == "DefaultChordLow")
                        {
                            return;
                        }

                        // Trigger chord low hand shape
                        SetTrigger("DefaultChordLow");
                        _currentChordShape = "DefaultChordLow";
                    }
                }
                else
                {
                    if (isSustain || _alwaysBend)
                    {
                        if (_currentChordShape == "DefaultChordHighVibrato")
                        {
                            return;
                        }

                        // Trigger chord high vibrato hand shape
                        SetTrigger("DefaultChordHighVibrato");
                        _currentChordShape = "DefaultChordHighVibrato";
                        SetDelayedTrigger("DefaultChordHigh", sustainLength);
                    }
                    else
                    {
                        if (_currentChordShape == "DefaultChordHigh")
                        {
                            return;
                        }

                        // Trigger chord high hand shape
                        SetTrigger("DefaultChordHigh");
                        _currentChordShape = "DefaultChordHigh";
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
                        if (_currentChordShape == "DefaultSingleLowVibrato")
                        {
                            return;
                        }

                        // Trigger single note low vibrato hand shape
                        SetTrigger("DefaultSingleLowVibrato");
                        _currentChordShape = "DefaultSingleLowVibrato";
                        SetDelayedTrigger("DefaultSingleLow", sustainLength);
                    }
                    else
                    {
                        if (_currentChordShape == "DefaultSingleLow")
                        {
                            return;
                        }

                        // Trigger single note low hand shape
                        SetTrigger("DefaultSingleLow");
                        _currentChordShape = "DefaultSingleLow";
                    }
                }
                else
                {
                    if (isSustain || _alwaysBend)
                    {
                        if (_currentChordShape == "DefaultSingleHighVibrato")
                        {
                            return;
                        }

                        // Trigger single note high vibrato hand shape
                        SetTrigger("DefaultSingleHighVibrato");
                        _currentChordShape = "DefaultSingleHighVibrato";
                        SetDelayedTrigger("DefaultSingleHigh", sustainLength);
                    }
                    else
                    {


                        if (_currentChordShape == "DefaultSingleHigh")
                        {
                            return;
                        }

                        // Trigger single note high hand shape
                        SetTrigger("DefaultSingleHigh");
                        _currentChordShape = "DefaultSingleHigh";
                    }
                }
            }
            else
            {
                // We either have an open note or we have green and are using a map that uses open fingering for green
                // HandleDropD(isSustain, sustainLength);

                if (_handMap == HandMap.HandMapDropD || _handMap == HandMap.HandMapDropD2)
                {
                    if (_currentChordShape == "DropDOpen")
                    {
                        return;
                    }

                    SetTrigger("DropDOpen");
                    _currentChordShape = "DropDOpen";
                    // YargLogger.LogDebug("Drop D open hand shape triggered");

                    return;
                }

                if (_currentChordShape == "DefaultOpen")
                {
                    return;
                }
                // Trigger open hand shape
                SetTrigger("DefaultOpen");
                _currentChordShape = "DefaultOpen";
            }
        }

        private bool HandleDropD(bool isSustain, float sustainLength)
        {
            // YargLogger.LogDebug("HandleDropD called");
            if (_handMap == HandMap.HandMapDropD || _handMap == HandMap.HandMapDropD2)
            {
                // YargLogger.LogDebug("Drop D hand shape");
                if (isSustain)
                {
                    if (_currentChordShape == "DropDVibrato")
                    {
                        return true;
                    }
                    SetTrigger("DropDVibrato");
                    _currentChordShape = "DropDVibrato";
                    SetDelayedTrigger("DropD", sustainLength);
                    // YargLogger.LogDebug("Drop D vibrato hand shape triggered");
                    return true;
                }

                if (_currentChordShape == "DropD")
                {
                    return true;
                }

                // Trigger drop D hand shape
                SetTrigger("DropD");
                _currentChordShape = "DropD";
                // YargLogger.LogDebug("Returned to DropD hand shape");
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

            // _animationSpeed = 0;
            // _animator.SetFloat(_animationParamHash, 0);
            // _animator.Play("Base Layer.Idle");
            // _animator.Play(_idleAnimationHash);

            // We have to delay by TimeToFirstHit because we get called that amount early
            // if (Type != CharacterType.Bass)
            // {
            //     DOVirtual.DelayedCall(TimeToFirstHit, () => _animator.CrossFadeInFixedTime(_idleAnimationHash, 0.25f));
            // }
            // _animator.CrossFadeInFixedTime(_idleAnimationHash, 0.1f);
            // YargLogger.LogDebug("Starting Idle animation");

            if (!ChartHasAnimations)
            {
                SetTrigger(_idleAnimationName);
            }

            _isAnimating = false;
        }

        public void StartAnimation(float secondsPerBeat)
        {
            if (!_enableAnimationStates)
            {
                return;
            }

            _isAnimating = true;

            UpdateTempo(secondsPerBeat);

            // TODO: We actually need to check whether this character has advanced animations or not
            // If it does, and it is not a drums chart with no animations, we need to set playing/idle
            if (!ChartHasAnimations && Type == CharacterType.Drums)
            {
                SetTrigger(_playingAnimationName);
            }

            // _animator.Play("91e81ab3-0a89-48c2-b8ce-a23f28bdf736 Skeleton_Merged_mixamo_com_001,BaseLayer_91e81ab3-0a89-48c2-b8ce-a23f28b");
            // _animator.Play(_playingAnimationHash);
            // if (Type != CharacterType.Bass)
            // {
            //     _animator.CrossFadeInFixedTime(_playingAnimationHash, 0.1f);
            // }
            // YargLogger.LogDebug("Starting Strum animation");
        }

        public bool IsAnimating()
        {
            return _isAnimating;
        }

        public void UpdateTempo(float secondsPerBeat)
        {
            // Adjust the speed of the animation based on the song tempo
            if (_actionsPerAnimationCycle == 0)
            {
                return;
            }

            // We want one animation cycle per beat (as a multiplier, so if there are 17 actions and 0.4 seconds per beat, we want the animation to complete in 6.8 seconds)
            // var secondsPerAction = _animationLength / _actionsPerAnimationCycle;
            // var speed = secondsPerBeat / secondsPerAction;
            var secondsPerAction = _animationLength / _actionsPerAnimationCycle;
            // We want secondsPerAction to match secondsPerBeat
            var speed = secondsPerAction / secondsPerBeat;
            //
            // var desiredAnimationLength = secondsPerBeat * _actionsPerAnimationCycle;
            // var speed = _animationLength / desiredAnimationLength;

            // Not sure if this will work, but it's worth a try...
            if (speed >= 1.5)
            {
                speed /= 2;
            } else if (speed <= 0.6)
            {
                speed *= 2;
            }

            _animationSpeed = speed;
            _animator.SetFloat(_animationParamHash, speed);

            // YargLogger.LogFormatDebug("Adjusting speed of {0} to {1}", Type, speed);
        }

        private void OnAnimatorIK()
        {
            if (!_animator)
            {
                return;
            }

            _leftHandObject = _ikTargets[_currentLeftHandPosition];

            if (_ikActive && _leftHandObject != null)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
                _animator.SetIKPosition(AvatarIKGoal.LeftHand, _leftHandObject.position);
            }
            else
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0);
            }
        }

        private void SetDelayedTrigger(string triggerName, float delay)
        {
            _delayedTriggerName = triggerName;
            _delayedTriggerTime = delay;
        }

        private void CancelDelayedTrigger()
        {
            _delayedTriggerName = null;
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
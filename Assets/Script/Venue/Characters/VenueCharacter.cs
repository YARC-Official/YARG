using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Gameplay.Visuals;
using AnimationEvent = YARG.Core.Chart.AnimationEvent;
using AnimationTrigger = YARG.Venue.Characters.CharacterManager.AnimationTrigger;
using AnimationState = YARG.Venue.Characters.CharacterManager.AnimationState;
using HandMap = YARG.Venue.Characters.CharacterManager.HandMap;
using StrumMap = YARG.Venue.Characters.CharacterManager.StrumMap;

#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

namespace YARG.Venue.Characters
{
    [RequireComponent(typeof(Animator))]
    public class VenueCharacter : MonoBehaviour
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
        private Dictionary<AnimationStates, string> _animationStates;

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

        private bool _ikActive;
        private Transform _leftHandObject;

        private HashSet<string>                                              _availableAnimations      = new();
        private Dictionary<int, string>                                      _animationHashToAnimation = new();
        private Dictionary<AnimationEvent.AnimationType, AnimationEventInfo> _animationLookup          = new();

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

        private Dictionary<string, int> _leftHandPositionHashes = new();
        #nullable enable
        private Dictionary<string, Transform?> _ikTargets = new();
        #nullable disable

        [NonSerialized]
        public float TimeToFirstHit = 0.0f;

        private string   _currentChordShape;

        private AnimationState _animationState;
        private HandMap        _handMap;
        private StrumMap       _strumMap;

        private bool _inhibitHandShape;
        private bool _isBend;
        private bool _handDown;

        private string _delayedTriggerName;
        private float  _delayedTriggerTime;

        private bool _alwaysBend => _handMap == HandMap.HandMapAllBend;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _animatorController = _animator.runtimeAnimatorController;

            // Get the available animations so we don't try to call ones the venue author didn't implement
            foreach (var animation in _animatorController.animationClips)
            {
                _availableAnimations.Add(animation.name);
            }
#if false
            var realController = _animatorController as AnimatorController;
            // Thankfully we know we want layer 0 for this debugging task
            if (realController != null && Type == CharacterType.Bass)
            {
                var layer = realController.layers[0];
                foreach (var state in layer.stateMachine.states)
                {
                    _animationHashToAnimation.Add(state.state.name.GetHashCode(), state.state.name);
                }
            }
#endif
            // Get the available layers so we don't try to call ones the venue author didn't implement
            // if (_animatorController is AnimatorController controller)
            // {
            //     foreach (var layer in controller.layers)
            //     {
            //         _availableLayers.Add(layer.name);
            //     }
            //
            //     foreach (var parameter in controller.parameters)
            //     {
            //         _availableParameters.Add(parameter.name);
            //     }
            // }

            // _animationLookup = BuildAnimationLookup();

            // Start in the idle state
            // _animator.Play(_animationStates[AnimationStates.Idle]);

            // Figure out how long the animation is
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

        private void HandleStrumMap(CharacterManager.StrumMap strumMap)
        {
            // Strum map is only valid for bass
            if (Type != CharacterType.Bass)
            {
                return;
            }

            _strumTrigger = strumMap switch
            {
                CharacterManager.StrumMap.StrumMapPick => "StrumPick",
                CharacterManager.StrumMap.StrumMapSlapBass => "StrumSlap",
                _ => "Strum"
            };
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
                        _animator.SetTrigger("ChordA");
                        _inhibitHandShape = true;
                        YargLogger.LogDebug("Chord A triggered");
                        break;
                    case HandMap.HandMapChordC:
                        _animator.SetTrigger("ChordC");
                        _inhibitHandShape = true;
                        YargLogger.LogDebug("Chord C triggered");
                        break;
                    case HandMap.HandMapChordD:
                        _animator.SetTrigger("ChordD");
                        _inhibitHandShape = true;
                        YargLogger.LogDebug("Chord D triggered");
                        break;
                    case HandMap.HandMapDropD:
                        _animator.SetTrigger("DropD");
                        _inhibitHandShape = true;
                        YargLogger.LogDebug("Drop D triggered");
                        break;
                    case HandMap.HandMapDropD2:
                        _animator.SetTrigger("DropD");
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
                    _strumMap = animation.StrumMap;
                    break;
            }
        }

        public void OnGuitarAnimation(AnimationEvent.AnimationType animation)
        {
            var animName = animation switch
            {
                AnimationEvent.AnimationType.LeftHandPosition1 => "HandPositionOne",
                AnimationEvent.AnimationType.LeftHandPosition2 => "HandPositionOne",
                AnimationEvent.AnimationType.LeftHandPosition3 => "HandPositionTwo",
                AnimationEvent.AnimationType.LeftHandPosition4 => "HandPositionTwo",
                AnimationEvent.AnimationType.LeftHandPosition5 => "HandPositionThree",
                AnimationEvent.AnimationType.LeftHandPosition6 => "HandPositionThree",
                AnimationEvent.AnimationType.LeftHandPosition7 => "HandPositionFour",
                AnimationEvent.AnimationType.LeftHandPosition8 => "HandPositionFour",
                AnimationEvent.AnimationType.LeftHandPosition9 => "HandPositionFive",
                AnimationEvent.AnimationType.LeftHandPosition10 => "HandPositionFive",
                AnimationEvent.AnimationType.LeftHandPosition11 => "HandPositionSix",
                AnimationEvent.AnimationType.LeftHandPosition12 => "HandPositionSix",
                AnimationEvent.AnimationType.LeftHandPosition13 => "HandPositionSeven",
                AnimationEvent.AnimationType.LeftHandPosition14 => "HandPositionSeven",
                AnimationEvent.AnimationType.LeftHandPosition15 => "HandPositionEight",
                AnimationEvent.AnimationType.LeftHandPosition16 => "HandPositionEight",
                _ => "HandPositionEight" // We haven't gotten any farther yet
            };

            // YargLogger.LogDebug($"Animation {animName} triggered");

            _currentLeftHandPosition = animName;
            // _animator.CrossFadeInFixedTime(animName, 0.1f, _leftHandLayerIndex);
            _animator.CrossFadeInFixedTime(animName, 0.1f);
            // _animator.Play(animName, _leftHandLayerIndex);
            // _animator.SetTrigger(animName);
        }

        public void OnDrumAnimation(AnimationEvent.AnimationType animation)
        {
            var animName = animation switch
            {
                AnimationEvent.AnimationType.Kick => "Kick",
                AnimationEvent.AnimationType.OpenHiHat => "OpenHat",
                AnimationEvent.AnimationType.CloseHiHat => "CloseHat",
                AnimationEvent.AnimationType.HihatLeftHand => "HihatLeft",
                AnimationEvent.AnimationType.HihatRightHand => "HihatRight",
                AnimationEvent.AnimationType.SnareLhHard => "SnareLeft",
                AnimationEvent.AnimationType.SnareRhHard => "SnareRight",
                AnimationEvent.AnimationType.SnareLhSoft => "SnareLeft",
                AnimationEvent.AnimationType.SnareRhSoft => "SnareRight",
                AnimationEvent.AnimationType.Crash1LhHard => "Crash1Left",
                AnimationEvent.AnimationType.Crash1LhSoft => "Crash1Left",
                AnimationEvent.AnimationType.Crash1RhHard => "Crash1Right",
                AnimationEvent.AnimationType.Crash1RhSoft => "Crash1Left",
                AnimationEvent.AnimationType.Crash2LhHard => "Crash2Left",
                AnimationEvent.AnimationType.Crash2LhSoft => "Crash2Left",
                AnimationEvent.AnimationType.Crash2RhHard => "Crash2Right",
                AnimationEvent.AnimationType.Crash2RhSoft => "Crash2Right",
                AnimationEvent.AnimationType.RideLh => "RideLeft",
                AnimationEvent.AnimationType.RideRh => "RideRight",
                AnimationEvent.AnimationType.Tom1LeftHand => "Tom1Left",
                AnimationEvent.AnimationType.Tom1RightHand => "Tom1Right",
                AnimationEvent.AnimationType.Tom2LeftHand => "Tom2Left",
                AnimationEvent.AnimationType.Tom2RightHand => "Tom2Right",
                AnimationEvent.AnimationType.FloorTomLeftHand => "FloorTomLeft",
                AnimationEvent.AnimationType.FloorTomRightHand => "FloorTomRight",
                _ => null
            };

            int? layerIndex = animation switch
            {
                AnimationEvent.AnimationType.Kick           => _kickLayerIndex,
                AnimationEvent.AnimationType.OpenHiHat      => _hatLayerIndex,
                AnimationEvent.AnimationType.CloseHiHat     => _hatLayerIndex,
                AnimationEvent.AnimationType.HihatLeftHand  => _leftHandLayerIndex,
                AnimationEvent.AnimationType.HihatRightHand => _rightHandLayerIndex,
                AnimationEvent.AnimationType.SnareLhHard    => _leftHandLayerIndex,
                AnimationEvent.AnimationType.SnareRhSoft    => _rightHandLayerIndex,
                AnimationEvent.AnimationType.SnareLhSoft    => _leftHandLayerIndex,
                AnimationEvent.AnimationType.SnareRhHard    => _rightHandLayerIndex,
                AnimationEvent.AnimationType.Crash1LhHard   => _leftHandLayerIndex,
                AnimationEvent.AnimationType.Crash1LhSoft   => _leftHandLayerIndex,
                AnimationEvent.AnimationType.Crash1RhHard   => _rightHandLayerIndex,
                AnimationEvent.AnimationType.Crash1RhSoft   => _rightHandLayerIndex,
                AnimationEvent.AnimationType.Crash2LhHard   => _leftHandLayerIndex,
                AnimationEvent.AnimationType.Crash2LhSoft   => _leftHandLayerIndex,
                AnimationEvent.AnimationType.Crash2RhHard   => _rightHandLayerIndex,
                AnimationEvent.AnimationType.Crash2RhSoft   => _rightHandLayerIndex,
                AnimationEvent.AnimationType.RideLh         => _leftHandLayerIndex,
                AnimationEvent.AnimationType.RideRh         => _rightHandLayerIndex,
                AnimationEvent.AnimationType.Tom1LeftHand   => _leftHandLayerIndex,
                AnimationEvent.AnimationType.Tom1RightHand  => _rightHandLayerIndex,
                AnimationEvent.AnimationType.Tom2LeftHand   => _leftHandLayerIndex,
                AnimationEvent.AnimationType.Tom2RightHand  => _rightHandLayerIndex,
                AnimationEvent.AnimationType.FloorTomLeftHand => _leftHandLayerIndex,
                AnimationEvent.AnimationType.FloorTomRightHand => _rightHandLayerIndex,
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
            _animator.SetTrigger(animName);

        }

        public void OnNote<T>(Note<T> note) where T : Note<T>
        {

            if (note is GuitarNote gNote)
            {
                // Strum animations are only for bass rn
                if (Type == CharacterType.Bass && gNote.IsStrum)
                {
                    // Figure out which way to strum...I guess by looking at which state we're currently in?
                    // TODO: Don't hardcode the layer index
                    var currentState = _animator.GetCurrentAnimatorStateInfo(0);
                    var nextState = _animator.GetNextAnimatorStateInfo(0);
                    var currentClip = _animator.GetCurrentAnimatorClipInfo(0)[0].clip.name;

                    // YargLogger.LogDebug($"Current clip is {currentClip}");

                    // Hand is down, so strum up
                    // if (currentClip == "BassStrumDown" || currentClip == "BassIdleDown")
                    if (_handDown)
                    {
                        // YargLogger.LogDebug("Strum up animation triggered");
                        _animator.SetTrigger("StrumUp");
                        _handDown = false;
                    }
                    else
                    {
                        // Hand isn't down, so strum down
                        // YargLogger.LogDebug("Strum down animation triggered");
                        _animator.SetTrigger("StrumDown");
                        _handDown = true;
                    }
                }

                SetHandAnimationForNote(gNote);

            }

            // if (note is DrumNote)
            // {
            //     bool hasKick = false;
            //     foreach (var child in note.AllNotes)
            //     {
            //         if (child is DrumNote { Pad: (int) FourLaneDrumPad.Kick })
            //         {
            //             hasKick = true;
            //             break;
            //         }
            //     }
            //
            //     if (hasKick)
            //     {
            //         _animator.Play("Kick", _kickLayerIndex);
            //         YargLogger.LogDebug("Kick drum animation started");
            //     }
            // }

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
                        _animator.SetTrigger("DefaultChordLowVibrato");
                        SetDelayedTrigger("DefaultChordLow", sustainLength);
                    }
                    else
                    {
                        if (_currentChordShape == "DefaultChordLow")
                        {
                            return;
                        }

                        // Trigger chord low hand shape
                        _animator.SetTrigger("DefaultChordLow");
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
                        _animator.SetTrigger("DefaultChordHighVibrato");
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
                        _animator.SetTrigger("DefaultChordHigh");
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
                        _animator.SetTrigger("DefaultSingleLowVibrato");
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
                        _animator.SetTrigger("DefaultSingleLow");
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
                        _animator.SetTrigger("DefaultSingleHighVibrato");
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
                        _animator.SetTrigger("DefaultSingleHigh");
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

                    _animator.SetTrigger("DropDOpen");
                    _currentChordShape = "DropDOpen";
                    // YargLogger.LogDebug("Drop D open hand shape triggered");

                    return;
                }

                if (_currentChordShape == "DefaultOpen")
                {
                    return;
                }
                // Trigger open hand shape
                _animator.SetTrigger("DefaultOpen");
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
                    _animator.SetTrigger("DropDVibrato");
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
                _animator.SetTrigger("DropD");
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
            if (Type != CharacterType.Bass)
            {
                DOVirtual.DelayedCall(TimeToFirstHit, () => _animator.CrossFadeInFixedTime(_idleAnimationHash, 0.25f));
            }
            // _animator.CrossFadeInFixedTime(_idleAnimationHash, 0.1f);
            // YargLogger.LogDebug("Starting Idle animation");

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

            // _animator.Play("91e81ab3-0a89-48c2-b8ce-a23f28bdf736 Skeleton_Merged_mixamo_com_001,BaseLayer_91e81ab3-0a89-48c2-b8ce-a23f28b");
            // _animator.Play(_playingAnimationHash);
            if (Type != CharacterType.Bass)
            {
                _animator.CrossFadeInFixedTime(_playingAnimationHash, 0.1f);
            }
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

        /// <summary>
        /// Builds a lookup dictionary for AnimationTypes that only includes the animations
        /// available in the current venue
        /// </summary>
        /// <returns></returns>
        private Dictionary<AnimationEvent.AnimationType, AnimationEventInfo> BuildAnimationLookup()
        {
            var lookup = new Dictionary<AnimationEvent.AnimationType, AnimationEventInfo>();

            // Nothing we can do if we don't actually have an animatorcontroller
            // if (_animatorController is not AnimatorController c)
            // {
            //     return lookup;
            // }
            //
            // // Loop through all the layers in the controller, getting their state machine and states for each layer.
            // foreach (var layer in c.layers)
            // {
            //     foreach (var state in layer.stateMachine.states)
            //     {
            //         // Create the AnimationEventInfo for this state
            //         if (!TryGetAnimationTypeForName(state.state.name, out var animType))
            //         {
            //             continue;
            //         }
            //
            //         lookup.Add(animType, new AnimationEventInfo(animType, state.state.name, Animator.StringToHash(state.state.name), _animator.GetLayerIndex(layer.name)));
            //     }
            // }


            return lookup;
        }

        private bool TryGetAnimationTypeForName(string name, out AnimationEvent.AnimationType outVar)
        {
            // Deal with venues that don't have separate hard/soft animations
            var adjustedName = name switch
            {
                "SnareLeft" => "SnareLeftHard",
                "SnareRight" => "SnareRightHard",
                "Crash1Left" => "Crash1LeftHard",
                "Crash1Right" => "Crash1RightHard",
                "Crash2Left" => "Crash2LeftHard",
                "Crash2Right" => "Crash2RightHard",
                _ => name
            };

            AnimationEvent.AnimationType? animType = adjustedName switch
            {
                "Kick"            => AnimationEvent.AnimationType.Kick,
                "OpenHat"         => AnimationEvent.AnimationType.OpenHiHat,
                "CloseHat"        => AnimationEvent.AnimationType.CloseHiHat,
                "HihatLeft"       => AnimationEvent.AnimationType.HihatLeftHand,
                "HihatRight"      => AnimationEvent.AnimationType.HihatRightHand,
                "SnareLeft"       => AnimationEvent.AnimationType.SnareLhHard,
                "SnareLeftHard"   => AnimationEvent.AnimationType.SnareLhHard,
                "SnareLeftSoft"   => AnimationEvent.AnimationType.SnareLhSoft,
                "SnareRightHard"  => AnimationEvent.AnimationType.SnareRhHard,
                "SnareRightSoft"  => AnimationEvent.AnimationType.SnareRhSoft,
                "Crash1LeftHard"  => AnimationEvent.AnimationType.Crash1LhHard,
                "Crash1LeftSoft"  => AnimationEvent.AnimationType.Crash1LhSoft,
                "Crash1RightHard" => AnimationEvent.AnimationType.Crash1RhHard,
                "Crash1RightSoft" => AnimationEvent.AnimationType.Crash1RhSoft,
                "Crash1Choke"     => AnimationEvent.AnimationType.Crash1Choke,
                "Crash2LeftHard"  => AnimationEvent.AnimationType.Crash2LhHard,
                "Crash2LeftSoft"  => AnimationEvent.AnimationType.Crash2LhSoft,
                "Crash2RightHard" => AnimationEvent.AnimationType.Crash2RhHard,
                "Crash2RightSoft" => AnimationEvent.AnimationType.Crash2RhSoft,
                "Crash2Choke"     => AnimationEvent.AnimationType.Crash2Choke,
                "RideLeft"        => AnimationEvent.AnimationType.RideLh,
                "RideRight"       => AnimationEvent.AnimationType.RideRh,
                "Tom1Left"        => AnimationEvent.AnimationType.Tom1LeftHand,
                "Tom1Right"       => AnimationEvent.AnimationType.Tom1RightHand,
                "Tom2Left"        => AnimationEvent.AnimationType.Tom2LeftHand,
                "Tom2Right"       => AnimationEvent.AnimationType.Tom2RightHand,
                "FloorTomLeft"    => AnimationEvent.AnimationType.FloorTomLeftHand,
                "FloorTomRight"   => AnimationEvent.AnimationType.FloorTomRightHand,
                _                 => null
            };

            if (animType.HasValue)
            {
                outVar = animType.Value;
                return true;
            }

            outVar = default;
            return false;
        }

        private class AnimationEventInfo
        {
            public AnimationEventInfo(AnimationEvent.AnimationType animationType, string animationName, int hash, int layer)
            {
                AnimationType = animationType;
                AnimationName = animationName;
                Hash = hash;
                Layer = layer;
            }

            public readonly AnimationEvent.AnimationType AnimationType;
            public readonly string                       AnimationName;
            public readonly int                          Hash;
            public readonly int                          Layer;

        }
    }
}
using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Logging;
using AnimationEvent = YARG.Core.Chart.AnimationEvent;

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

        private string _currentLeftHandPosition;

        private Dictionary<string, int> _leftHandPositionHashes = new();
        #nullable enable
        private Dictionary<string, Transform?> _leftHandIKTargets = new();
        #nullable disable

        [NonSerialized]
        public float TimeToFirstHit = 0.0f;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _animatorController = _animator.runtimeAnimatorController;

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

                // Getting the index is broke, but we know it's 1, so we'll hardcode it for now
                _leftHandLayerIndex = 1;
            }

            GetPositionHashes();
            GetIKTargets();

        }

        private void GetIKTargets()
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
                    _leftHandIKTargets.Add(transform.name, transform);
                }
            }

            if (_leftHandIKTargets.Count > 0)
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

            YargLogger.LogDebug($"Animation {animName} triggered");

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
                AnimationEvent.AnimationType.OpenHiHat => "OpenHat", // TODO: This actually needs to trigger CloseHat when the note ends
                AnimationEvent.AnimationType.CloseHiHat => "CloseHat",
                _ => null
            };

            int? layerIndex = animation switch
            {
                AnimationEvent.AnimationType.Kick       => _kickLayerIndex,
                AnimationEvent.AnimationType.OpenHiHat  => _hatLayerIndex,
                AnimationEvent.AnimationType.CloseHiHat => _hatLayerIndex,
                _                                       => null
            };

            if (animName == null || layerIndex == null)
            {
                return;
            }

            if (animName == "OpenHat")
            {
                YargLogger.LogDebug("OpenHat animation triggered");
            } else if (animName == "CloseHat")
            {
                YargLogger.LogDebug("CloseHat animation triggered");
            }

            // _animator.CrossFadeInFixedTime(animName, 0.1f, layerIndex.Value);
            _animator.CrossFadeInFixedTime(animName, 0.1f);

        }

        public void OnNote<T>(Note<T> note) where T : Note<T>
        {
            if (note is Note<GuitarNote>)
            {

            }

            if (note is DrumNote)
            {
                bool hasKick = false;
                foreach (var child in note.AllNotes)
                {
                    if (child is DrumNote { Pad: (int) FourLaneDrumPad.Kick })
                    {
                        hasKick = true;
                        break;
                    }
                }

                if (hasKick)
                {
                    _animator.Play("Kick", _kickLayerIndex);
                    YargLogger.LogDebug("Kick drum animation started");
                }
            }

            if (note is Note<VocalNote>)
            {

            }

            if (note is Note<ProKeysNote>)
            {

            }
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
            DOVirtual.DelayedCall(TimeToFirstHit, () => _animator.CrossFadeInFixedTime(_idleAnimationHash, 0.25f));
            // _animator.CrossFadeInFixedTime(_idleAnimationHash, 0.1f);
            YargLogger.LogDebug("Starting Idle animation");

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
            _animator.CrossFadeInFixedTime(_playingAnimationHash, 0.1f);
            YargLogger.LogDebug("Starting Strum animation");
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

            YargLogger.LogFormatDebug("Adjusting speed of {0} to {1}", Type, speed);
        }

        private void OnAnimatorIK()
        {
            if (!_animator)
            {
                return;
            }

            _leftHandObject = _leftHandIKTargets[_currentLeftHandPosition];

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
    }
}
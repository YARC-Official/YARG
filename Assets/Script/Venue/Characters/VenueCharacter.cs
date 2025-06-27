using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Venue.Characters
{
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

        private int _idleAnimationHash;
        private int _playingAnimationHash;

        private RuntimeAnimatorController _animatorController;
        private Animator _animator;

        private float _animationLength;
        private float _animationSpeed;
        private int   _animationParamHash;

        private bool _isAnimating;

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
        }

        public void OnNote<T>(Note<T> note) where T : Note<T>
        {
            if (note is Note<GuitarNote>)
            {

            }

            // TODO: Make this MFer work with both four and five lane
            if (note is DrumNote { Pad: (int) FourLaneDrumPad.Kick })
            {
                _animator.Play("Base Layer.Kick");
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
            DOVirtual.DelayedCall(TimeToFirstHit, () => _animator.CrossFadeInFixedTime(_idleAnimationHash, 0.1f));
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
    }
}
using System;
using System.Collections.Generic;
using UniGLTF.SpringBoneJobs.Blittables;
using UnityEngine;
using UniVRM10;
using YARG.Core.Chart;
using YARG.Helpers;
using YARG.Song;
using LipsyncType = YARG.Core.Chart.LipsyncEvent.LipsyncType;
using YARG.Venue.VenueCamera;

namespace YARG.Venue.Characters
{
    public class VRMCharacter : VenueCharacter
    {
        private Vrm10RuntimeExpression _expression;
        private List<LipsyncEvent>     _lipsyncEvents;
        private int                    _lipsyncIndex;
        private List<PerformerEvent>   _singalongEvents;
        private int                    _singalongEventIndex;
        private bool                   _hasLipsyncAssigned;

        private ExpressionKey _browAggressive;
        private ExpressionKey _browDown;
        private ExpressionKey _browOpenmouthed;
        private ExpressionKey _squint;

        private readonly Dictionary<string, ExpressionKey> _customExpressions = new();



        [Header("Lipsync Settings")]
        [SerializeField]
        [Tooltip("The expression key to activate for basic lipsync.")]
        private ExpressionKeyKind _expressionKey = ExpressionKeyKind.Ou;
        [SerializeField]
        [Tooltip("Set to true if you have the full set of RB expressions implemented.\n\nOtherwise, lipsync will only use the selected VRM default expression key.")]
        private bool _useFullLipsync;
        [SerializeField]
        [Tooltip("Set to true if you want to use custom animations instead of the default ones.")]
        public bool UseCustomAnimations;
        [SerializeField]
        [Tooltip("Genre-specific custom animations")]
        private GenreAnimationMap _genreSpecificAnimations;

        private ExpressionKey       _lipsyncKey;
        private bool                _hasVrmInstance;
        private BlittableModelLevel _modelLevels;

        private Vector3 _initialPosition;
        private Quaternion _initialRotation;

        // For checking visibility
        private MeshRenderer _visibilityRenderer;
        private MeshFilter   _visibilityFilter;
        private Bounds       _visibilityBounds;
        private bool         _hasBounds;

        private bool _wasVisible;

        private static Mesh     _unitCubeMesh;
        private static Material _invisibleMaterial;

        private bool HasLipsyncEvents => _lipsyncEvents != null && _lipsyncEvents.Count > 0;

        public int ActionsPerAnimationCycle
        {
            get => _actionsPerAnimationCycle;
            set => _actionsPerAnimationCycle = value;
        }

        public int FramesToFirstHit
        {
            get => _framesToFirstHit;
            set => _framesToFirstHit = value;
        }

        private CameraManager _cameraManager;

        private bool _isCustomCharacter;

        public override void Initialize(CharacterManager characterManager = null, bool isCustom = false)
        {
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;

            // Find camera manager
            _cameraManager = FindFirstObjectByType<CameraManager>();
            // Subscribe to camera cut event
            _cameraManager.OnCameraCut += OnCameraCut;

            _isCustomCharacter = isCustom;

            _lipsyncKey = GetExpressionKey(_expressionKey);
            _characterManager = characterManager;
            VrmInstance = GetComponent<Vrm10Instance>();
            _hasVrmInstance = VrmInstance != null;
            _modelLevels = new BlittableModelLevel();
            _expression = VrmInstance.Runtime.Expression;
            _lipsyncEvents = new List<LipsyncEvent>();
            _singalongEvents = new List<PerformerEvent>();

            var clips = VrmInstance.Vrm.Expression.CustomClips;

            foreach (var clip in clips)
            {
                _customExpressions[clip.name] = VrmInstance.Vrm.Expression.CreateKey(clip);
            }

            base.Initialize(characterManager, isCustom);

            _rngHash = Animator.StringToHash("RNG");
            HasRng = _intHashes.Contains(_rngHash);
        }

        public void InitializeLipsync(List<LipsyncEvent> lipsyncEvents, List<PerformerEvent> singalongEvents)
        {
            _lipsyncEvents = lipsyncEvents;
            _singalongEvents = singalongEvents;
            _singalongEventIndex = 0;
            _lipsyncIndex = 0;
            _hasLipsyncAssigned = true;
        }

        public GenreAnimationMap GetGenreSpecificAnimations()
        {
            return _genreSpecificAnimations;
        }

        protected override void Update()
        {
            if (_characterManager != null)
            {
                ProcessLipsync(_characterManager.SongTime);
            }

            if (HasRng)
            {
                var random = UnityEngine.Random.Range(0, 9);
                _animator.SetInteger(_rngHash, random);
                CurrentRng = random;
            }

            base.Update();
        }

        private void ProcessLipsync(double time)
        {
            if (!_hasLipsyncAssigned)
            {
                return;
            }

            bool shouldSing = _singalongEvents.Count == 0 || (_singalongEventIndex < _singalongEvents.Count && _singalongEvents[_singalongEventIndex].Time <= time && time <= _singalongEvents[_singalongEventIndex].TimeEnd);
            while (_lipsyncIndex < _lipsyncEvents.Count && _lipsyncEvents[_lipsyncIndex].Time <= time)
            {
                var lipsyncEvent = _lipsyncEvents[_lipsyncIndex];
                if (shouldSing)
                {
                    SetExpression(lipsyncEvent);
                }
                _lipsyncIndex++;
            }

            while (_singalongEventIndex < _singalongEvents.Count && _singalongEvents[_singalongEventIndex].TimeEnd <= time)
            {
                _singalongEventIndex++;
                ResetExpressions();
            }
        }

        private void SetExpression(LipsyncEvent lipsyncEvent)
        {
            if (!_hasVrmInstance)
            {
                return;
            }

            if (TryGetExpressionKey(lipsyncEvent.Type, out var key))
            {
                _expression.SetWeight(key, lipsyncEvent.Value);
                return;
            }

            // Couldn't find a default expression, so look for customs
            if (TryGetExpressionKey(lipsyncEvent.Type.ToString(), out key))
            {
                _expression.SetWeight(key, lipsyncEvent.Value);
                return;
            }

            _expression.SetWeight(_lipsyncKey, lipsyncEvent.Value);
        }

        private void ResetExpressions()
        {
            foreach (var key in _customExpressions.Values)
            {
                _expression.SetWeight(key, 0f);
            }
            _expression.SetWeight(_lipsyncKey, 0f);
        }

        public void SetWind(Vector3 wind)
        {
            if (!_hasVrmInstance)
            {
                return;
            }

            //update external force
            _modelLevels = new BlittableModelLevel(externalForce: wind,
                stopSpringBoneWriteback: _modelLevels.StopSpringBoneWriteback,
                supportsScalingAtRuntime: _modelLevels.SupportsScalingAtRuntime);
            //push model level changes to VRM runtime
            VrmInstance.Runtime.SpringBone.SetModelLevel(VrmInstance.transform, _modelLevels);
        }

        public void SetSpringPause(bool paused)
        {
            if (!_hasVrmInstance)
            {
                return;
            }
            //set spring bone paused state
            _modelLevels = new BlittableModelLevel(externalForce: _modelLevels.ExternalForce,
                stopSpringBoneWriteback: paused,
                supportsScalingAtRuntime: _modelLevels.SupportsScalingAtRuntime);
            //push model level changes to VRM runtime
            VrmInstance.Runtime.SpringBone.SetModelLevel(VrmInstance.transform, _modelLevels);
        }

        private void OnCameraCut()
        {
            // Only for vocals for now
            // TODO: Remove the _isCustomCharacter check once the venues are updated
            if (Type != CharacterType.Vocals || !_isCustomCharacter)
            {
                return;
            }

            // keep spring bones from flailing when we move the character
            SetSpringPause(true);

            // Trigger default animation from controller (so we don't end up with the character floating or something)
            ResetGenericTriggers();

            // Reset x and z pos to initial
            transform.position = _initialPosition;
            transform.rotation = _initialRotation;

            // Retrigger current animation state
            SetTrigger(CurrentGenericState);

            SetSpringPause(false);
        }

        public override void OnChartEvent(ChartEvent e)
        {

        }

        private static bool IsMouthShape(LipsyncType type)
        {
            return type switch
            {
                LipsyncType.Bump_hi    => true,
                LipsyncType.Bump_lo    => true,
                LipsyncType.Cage_hi    => true,
                LipsyncType.Cage_lo    => true,
                LipsyncType.Church_hi  => true,
                LipsyncType.Church_lo  => true,
                LipsyncType.Earth_hi   => true,
                LipsyncType.Earth_lo   => true,
                LipsyncType.Eat_hi     => true,
                LipsyncType.Eat_lo     => true,
                LipsyncType.Fave_hi    => true,
                LipsyncType.Fave_lo    => true,
                LipsyncType.If_hi      => true,
                LipsyncType.If_lo      => true,
                LipsyncType.Neutral_hi => true,
                LipsyncType.Neutral_lo => true,
                LipsyncType.New_hi     => true,
                LipsyncType.New_lo     => true,
                LipsyncType.Oat_hi     => true,
                LipsyncType.Oat_lo     => true,
                LipsyncType.Ox_hi      => true,
                LipsyncType.Ox_lo      => true,
                LipsyncType.Roar_hi    => true,
                LipsyncType.Roar_lo    => true,
                LipsyncType.Size_hi    => true,
                LipsyncType.Size_lo    => true,
                LipsyncType.Though_hi  => true,
                LipsyncType.Though_lo  => true,
                LipsyncType.Told_hi    => true,
                LipsyncType.Told_lo    => true,
                LipsyncType.Wet_hi     => true,
                LipsyncType.Wet_lo     => true,
                _                      => false
            };
        }

        public enum ExpressionKeyKind
        {
            Aa,
            Ih,
            Ou,
            Ee,
            Oh,
        }

        private static ExpressionKey GetExpressionKey(ExpressionKeyKind kind)
        {
            return kind switch
            {
                ExpressionKeyKind.Aa => ExpressionKey.Aa,
                ExpressionKeyKind.Ih => ExpressionKey.Ih,
                ExpressionKeyKind.Ee => ExpressionKey.Ee,
                ExpressionKeyKind.Ou => ExpressionKey.Ou,
                ExpressionKeyKind.Oh => ExpressionKey.Oh,
                _                    => throw new ArgumentException("Invalid expression key kind"),
            };
        }

        private bool TryGetExpressionKey(string keyName, out ExpressionKey key)
        {
            key = default;

            if (_customExpressions.TryGetValue(keyName, out key))
            {
                return true;
            }

            return false;
        }

        private static bool TryGetExpressionKey(LipsyncType type, out ExpressionKey key)
        {
            ExpressionKey? possibleKey = type switch
            {
                LipsyncType.Blink => ExpressionKey.Blink,
                _ => null
            };

            if (possibleKey.HasValue)
            {
                key = possibleKey.Value;
                return true;
            }

            key = default;

            return false;
        }

        private void OnDestroy()
        {
            if (_cameraManager == null)
            {
                return;
            }

            _cameraManager.OnCameraCut -= OnCameraCut;
        }
    }
}
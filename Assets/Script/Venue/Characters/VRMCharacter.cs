using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UniVRM10;
using YARG.Core.Chart;
using YARG.Core.Logging;
using LipsyncType = YARG.Core.Chart.LipsyncEvent.LipsyncType;
using YARG.Gameplay;
using YARG.Venue.VenueCamera;

namespace YARG.Venue.Characters
{
    public class VRMCharacter : VenueCharacter
    {
        private Vrm10RuntimeExpression _expression;
        private List<LipsyncEvent>     _lipsyncEvents;

        private ExpressionKey _browAggressive;
        private ExpressionKey _browDown;
        private ExpressionKey _browOpenmouthed;
        private ExpressionKey _squint;

        private readonly Dictionary<string, ExpressionKey> _customExpressions = new();

        private int _lipsyncIndex;

        [Header("Lipsync Settings")]
        [SerializeField]
        [Tooltip("The expression key to activate for basic lipsync.")]
        private ExpressionKeyKind _expressionKey = ExpressionKeyKind.Ou;
        [SerializeField]
        [Tooltip("Set to true if you have the full set of RB expressions implemented.\n\nOtherwise, lipsync will only use the selected VRM default expression key.")]
        private bool _useFullLipsync;

        private ExpressionKey _lipsyncKey;
        private bool          _hasVrmInstance;

        private Vector3 _initialPosition;

        // For checking visibility
        private MeshRenderer _visibilityRenderer;
        private MeshFilter   _visibilityFilter;
        private Bounds       _visibilityBounds;
        private bool         _hasBounds;

        private bool _wasVisible;

        private static Mesh     _unitCubeMesh;
        private static Material _invisibleMaterial;

        private bool HasLipsyncEvents => _lipsyncEvents != null && _lipsyncEvents.Count > 0;

        private CameraManager _cameraManager;

        public override void Initialize(CharacterManager characterManager)
        {
            _initialPosition = transform.position;

            // Find camera manager
            _cameraManager = FindFirstObjectByType<CameraManager>();

            SetupBoundsCheck();

            _lipsyncKey = GetExpressionKey(_expressionKey);
            _characterManager = characterManager;
            VrmInstance = GetComponent<Vrm10Instance>();
            _hasVrmInstance = VrmInstance != null;
            _expression = VrmInstance.Runtime.Expression;
            _lipsyncEvents = _characterManager.LipsyncEvents;

            var clips = VrmInstance.Vrm.Expression.CustomClips;

            foreach (var clip in clips)
            {
                _customExpressions[clip.name] = VrmInstance.Vrm.Expression.CreateKey(clip);
            }

            base.Initialize(characterManager);
        }

        protected override void Update()
        {
            ProcessLipsync(_characterManager.SongTime);
            base.Update();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            UpdateBounds();
        }

        private void ProcessLipsync(double time)
        {
            // We may have initialized too early, so we need to protect against null reference and hope it fixes itself later
            if (_characterManager.LipsyncEvents == null)
            {
                return;
            }

            while (_lipsyncIndex < _characterManager.LipsyncEvents.Count && _characterManager.LipsyncEvents[_lipsyncIndex].Time <= time)
            {
                var lipsyncEvent = _characterManager.LipsyncEvents[_lipsyncIndex];

                SetExpression(lipsyncEvent);

                _lipsyncIndex++;
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
            }
        }

        public override void OnNote<T>(Note<T> note)
        {
            // If _useFullLipsync is set, we don't use the default expression or the note-based trigger
            // ...unless the chart doesn't have lipsync events
            if (!_hasVrmInstance || (_useFullLipsync && HasLipsyncEvents))
            {
                return;
            }

            if (note is VocalNote vocalNote)
            {
                // Animate in/out of expression for animLength time
                float animLength = (float) vocalNote.TotalTimeLength * 0.1f;
                float offDelay = (float) vocalNote.TotalTimeLength * 0.9f;

                var currentWeight = _expression.GetWeight(_lipsyncKey);
                DOVirtual.Float(currentWeight, 1f, animLength, x => _expression.SetWeight(_lipsyncKey, x))
                    .SetAutoKill(true);
                DOVirtual.DelayedCall(offDelay, () => ExpressionOff(_lipsyncKey, animLength));
            }
        }

        private void ExpressionOff(ExpressionKey key, float length)
        {
            if (length > 0)
            {
                DOVirtual.Float(_expression.GetWeight(key), 0f, length, x => _expression.SetWeight(key, x)).SetAutoKill(true);
                return;
            }

            _expression.SetWeight(key, 0f);
        }

        private void SetupBoundsCheck()
        {
            if (_visibilityRenderer != null)
            {
                return;
            }

            SetupBoundsCheckResources();

            var boundsObject = new GameObject("Bounds Checker");
            boundsObject.transform.SetParent(transform, false);
            boundsObject.AddComponent<VisibilityForwarder>().Initialize(this);
            boundsObject.layer = LayerMask.NameToLayer("Venue");

            _visibilityRenderer = boundsObject.AddComponent<MeshRenderer>();
            _visibilityFilter = boundsObject.AddComponent<MeshFilter>();

            _visibilityFilter.sharedMesh = _unitCubeMesh;
            _visibilityRenderer.sharedMaterial = _invisibleMaterial;
            _visibilityRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _visibilityRenderer.receiveShadows = false;
            _visibilityRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _visibilityRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _visibilityRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            _visibilityRenderer.transform.localPosition = Vector3.zero;
            _visibilityRenderer.transform.localRotation = Quaternion.identity;
            _visibilityRenderer.transform.localScale = Vector3.one;

            _wasVisible = false;
            _hasBounds = false;
            _visibilityBounds = default;
        }

        private static void SetupBoundsCheckResources()
        {
            if (_unitCubeMesh == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _unitCubeMesh = tmp.GetComponent<MeshFilter>().mesh;
                Destroy(tmp);
            }

            if (_invisibleMaterial == null)
            {
                _invisibleMaterial = new Material(Shader.Find("Shader Graphs/LitFadeTransparent")) { color = Color.clear };
            }
        }

        private void UpdateBounds()
        {
            var renderers = GetComponentsInChildren<Renderer>(false);

            bool hasAny = false;
            Bounds worldBounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r == _visibilityRenderer)
                {
                    continue;
                }

                if (!hasAny)
                {
                    worldBounds = r.bounds;
                    hasAny = true;
                }
                else
                {
                    worldBounds.Encapsulate(r.bounds);
                }
            }

            _hasBounds = hasAny;
            _visibilityBounds = worldBounds;

            if (hasAny)
            {
                _visibilityRenderer.transform.position = worldBounds.center;
                _visibilityRenderer.transform.rotation = Quaternion.identity;
                _visibilityRenderer.transform.localScale = worldBounds.size;
            }
        }

        private void OnBecameVisible()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            _wasVisible = true;
        }

        private void OnBecameInvisible()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!_wasVisible)
            {
                return;
            }

            _wasVisible = false;

            if (!_hasBounds)
            {
                return;
            }

            var cam = _cameraManager?.CurrentCamera;
            if (cam == null)
            {
                return;
            }

            Vector3 destination = new Vector3(_initialPosition.x, transform.position.y, _initialPosition.z);

            if (WouldBeVisible(cam, _visibilityBounds))
            {
                return;
            }

            // Reset X and Z pos to their initial values
            transform.position = destination;
        }

        private static bool WouldBeVisible(Camera cam, Bounds bounds)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
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

        private sealed class VisibilityForwarder : MonoBehaviour
        {
            private VRMCharacter _owner;

            public void Initialize(VRMCharacter owner)
            {
                _owner = owner;
            }

            private void OnBecameVisible()
            {
                _owner?.OnBecameVisible();
            }

            private void OnBecameInvisible()
            {
                _owner?.OnBecameInvisible();
            }
        }
    }
}
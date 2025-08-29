using System;
using UnityEngine;
using UnityEngine.Serialization;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.Visuals
{
    public class TrackEffectElement : TrackElement<TrackPlayer>
    {
        [SerializeField]
        private MeshRenderer _meshRenderer;
        [FormerlySerializedAs("DisableStartTransition")]
        [SerializeField]
        private bool EnableStartTransition;
        [FormerlySerializedAs("DisableEndTransition")]
        [SerializeField]
        private bool EnableEndTransition;
        [SerializeField]
        private Material _soloTrackMaterial;
        [SerializeField]
        private Material _soloRailMaterial;
        [SerializeField]
        private Material _soloTrimMaterial;
        [SerializeField]
        private Material _soloTrimTransitionMaterial;
        [SerializeField]
        private Material _soloTransitionMaterial;
        [SerializeField]
        private Material _soloRailLeftTransitionMaterial;
        [SerializeField]
        private Material _soloRailRightTransitionMaterial;
        [SerializeField]
        private Material _unisonTrackMaterial;
        [SerializeField]
        private Material _unisonRailMaterial;
        [SerializeField]
        private Material _unisonTrimMaterial;
        [SerializeField]
        private Material _unisonTrimTransitionMaterial;
        [SerializeField]
        private Material _unisonTransitionMaterial;
        [SerializeField]
        private Material _unisonRailLeftTransitionMaterial;
        [SerializeField]
        private Material _unisonRailRightTransitionMaterial;
        [SerializeField]
        private Material _drumFillTrackMaterial;
        [SerializeField]
        private Material _drumFillRailMaterial;
        [SerializeField]
        private Material _drumFillTrimMaterial;
        [SerializeField]
        private Material _drumFillTrimTransitionMaterial;
        [SerializeField]
        private Material _drumFillTransitionMaterial;
        [SerializeField]
        private Material _drumFillRailLeftTransitionMaterial;
        [SerializeField]
        private Material _drumFillRailRightTransitionMaterial;

        [SerializeField]
        public float Visibility;

        private static readonly int _MaskDisabled = Shader.PropertyToID("_Mask_Disabled");
        private static readonly int _MaskMinZ = Shader.PropertyToID("_Mask_Min_Z");
        private static readonly int _MaskMaxZ = Shader.PropertyToID("_Mask_Max_Z");
        private static readonly int _MaskFadeDistance = Shader.PropertyToID("_Mask_Fade_Distance");

        public float StartVisibility;
        public float EndVisibility;

        // These are here to reflect the defaults set in the shader in case we need them in the future
        private float MaskDisabled = 1.0f;
        private float MaskFadeDistance = 0.2f;

        private bool _visibilityInTransition = false;
        private bool _maskEnabled = false;
        private float _currentVisibility = 1.0f;
        private float _currentEndZ = 0.0f;
        private float _currentStartVisibility = 1.0f;
        private float _currentEndVisibility = 1.0f;


        private bool _previousStartTransitionEnable;
        private bool _previousEndTransitionEnable;
        private double _visibilityStartTime;
        public TrackEffect EffectRef { get; set; }

        private static readonly int _visibility = Shader.PropertyToID("_Visibility");

        public override double ElementTime => EffectRef.Time;
        public double MiddleTime => EffectRef.Time + ((EffectRef.TimeEnd - EffectRef.Time) / 2);
        private float ZLength => (float) (EffectRef.TimeEnd - EffectRef.Time * Player.NoteSpeed);
        // not sure that we really need the +3.5
        private new float RemovePointOffset => (float) ((EffectRef.TimeEnd - EffectRef.Time) * Player.NoteSpeed + 3.5);

        private float StartZ => ZFromTime(EffectRef.Time);
        private float EndZ => ZFromTime(EffectRef.TimeEnd);

        public bool Active { get; private set; }

        protected override void InitializeElement()
        {
            EnableStartTransition = EffectRef.StartTransitionEnable;
            EnableEndTransition = EffectRef.EndTransitionEnable;
            _previousStartTransitionEnable = EffectRef.StartTransitionEnable;
            _previousEndTransitionEnable = EffectRef.EndTransitionEnable;
            Visibility = EffectRef.Visibility;
            StartVisibility = EnableStartTransition ? Visibility : 0.0f;
            EndVisibility = EnableEndTransition ? Visibility : 0.0f;
            _currentVisibility = Visibility;
            _currentStartVisibility = StartVisibility;
            _currentEndVisibility = EndVisibility;
            _visibilityInTransition = false;

            SetMaterials();
            RescaleForZ();
            InitializeMaterials();
            SetTransitionState();
            Active = true;
        }

        private void InitializeMaterials()
        {
            // This has to be done because it gets messed up when objects
            // get recycled. The effect is visually interesting, but not what is desired.

            // Get fade info
            float fadePos = Player.ZeroFadePosition;
            float fadeSize = Player.FadeSize;

            // Set all fade values for meshes
            var meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (var meshRenderer in meshRenderers)
            {
                foreach (var material in meshRenderer.materials)
                {
                    material.SetFade(fadePos, fadeSize);
                    material.SetFloat(_visibility, Visibility);
                }

                if (Visibility == 1.0f)
                {
                    // Enable the renderer in case it was disabled
                    // last time this poolable was used.
                    meshRenderer.enabled = true;
                }

                if (Visibility == 0.0f)
                {
                    // Disable the renderer, leaving the gameObject active
                    meshRenderer.enabled = false;
                }
            }
        }

        private void SetMaterials()
        {
            var children = GetComponentsInChildren<Renderer>(true);

            foreach (var child in children)
            {
                var newMaterial = GetMaterial(child.gameObject.name, EffectRef.EffectType);
                if (newMaterial == null)
                {
                    // Object should be disabled, so do that
                    child.gameObject.SetActive(false);
                    continue;
                }

                // This could have been disabled in a previous use of the element
                child.gameObject.SetActive(true);
                child.material = newMaterial;
            }
        }

        public void SetStartTransitionVisible(bool enable)
        {
            EnableStartTransition = enable;
            StartVisibility = enable ? 1.0f : 0.0f;
            SetTransitionState();
        }

        public void SetEndTransitionVisible(bool enable)
        {
            EnableEndTransition = enable;
            EndVisibility = enable ? 1.0f : 0.0f;
            SetTransitionState();
        }

        public void SetTransitionState()
        {
            var cachedTransform = _meshRenderer.transform;

            var children = cachedTransform.GetComponentsInChildren<Transform>(true);

            foreach (var child in children)
            {
                if (child == cachedTransform)
                {
                    continue;
                }

                if (child.gameObject.name != "TrackEffectStart" && child.gameObject.name != "TrackEffectEnd")
                {
                    continue;
                }

                if (child.gameObject.name is "TrackEffectStart")
                {
                    SetMaterialVisibility(child, StartVisibility);
                    if (!EnableStartTransition)
                    {
                        child.gameObject.SetActive(false);
                        continue;
                    }
                }

                if (child.gameObject.name is "TrackEffectEnd")
                {
                    SetMaterialVisibility(child, EndVisibility);
                    if (!EnableEndTransition)
                    {
                        child.gameObject.SetActive(false);
                        continue;
                    }
                }

                child.gameObject.SetActive(true);
            }
        }

        // We're taking a Transform because SetTransitionState uses them
        // and that's who we're expecting to call
        void SetMaterialVisibility(Transform target, float visibility)
        {
            var children = target.GetComponentsInChildren<Renderer>(true);
            foreach (var child in children)
            {
                if (child.material == null)
                {
                    continue;
                }
                child.material.SetFloat(_visibility, visibility);
            }
        }

        public void MakeVisible(bool enable = true)
        {
            _visibilityInTransition = true;
            _visibilityStartTime = GameManager.VisualTime;
            _maskEnabled = enable;
            _currentEndZ = StartZ;
            Visibility = enable ? 1.0f : 0.0f;
        }

        private void SetEffectMask(float endZ)
        {
            if (endZ >= EndZ)
            {
                _maskEnabled = false;
            }

            var disableMask = _maskEnabled ? 0.0f : 1.0f;
            var meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (var meshRenderer in meshRenderers)
            {
                if (meshRenderer.material == null)
                {
                    continue;
                }

                foreach (var material in meshRenderer.materials)
                {
                    material.SetFloat(_MaskDisabled, disableMask);
                    material.SetFloat(_MaskMinZ, StartZ);
                    material.SetFloat(_MaskMaxZ, endZ);
                }
            }
            _currentEndZ = endZ;
        }

        private void SetAllVisibility(float visibility)
        {
            if (Visibility == visibility)
            {
                _visibilityInTransition = false;
            }

            var meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (var meshRenderer in meshRenderers)
            {
                if (meshRenderer.material == null)
                {
                    continue;
                }
                foreach (var material in meshRenderer.materials)
                {
                    material.SetFloat(_visibility, visibility);
                }
                // Enable the renderer if it isn't already. Hopefully resetting it to the same value
                // if it happens to already be enabled isn't expensive.
                meshRenderer.enabled = true;
            }
            _currentVisibility = visibility;
        }

        protected override bool UpdateElementPosition()
        {
            // Calibration is not taken into consideration here, as that is instead handled in more
            // critical areas such as the game manager and players
            float z = ZFromTime(MiddleTime);

            var cacheTransform = transform;
            cacheTransform.localPosition = cacheTransform.localPosition.WithZ(z);

            if (z < REMOVE_POINT - RemovePointOffset)
            {
                ParentPool.Return(this);
                Active = false;
                return false;
            }

            return true;
        }

        protected void RescaleForZ()
        {
            // More correctly, this would get the unscaled size of the object
            // Since we currently use Unity's plane, this works
            const float zSize = 10.0f;
            float childZBasePosition = zSize / 2;
            var zScale = (float) (EffectRef.TimeEnd - EffectRef.Time) * Player.NoteSpeed / zSize;

            var cachedTransform = _meshRenderer.transform;

            // This is necessary to avoid the rescaling of the
            // parent from messing up the scaling of the children
            var children = cachedTransform.GetComponentsInChildren<Transform>();
            var scaleFactor = zScale / zSize;
            foreach (var child in children)
            {
                if (child == cachedTransform)
                {
                    continue;
                }

                // I'd combine these, but I think it would make it less readable

                // The point of all this is that only the transition parts need
                // special handling to remain correctly scaled since they are
                // supposed to remain fixed in size regardless of the length of
                // the track effect.
                if (child.gameObject.name is "TrackEffectTrimLeft" or "TrackEffectTrimRight")
                {
                    continue;
                }

                if (child.gameObject.name is "TrackEffectRailRight" or "TrackEffectRailLeft")
                {
                    continue;
                }

                if (child.gameObject.name is "TrackEffectTransitionRailLeft" or "TrackEffectTransitionRailRight")
                {
                    continue;
                }

                if (child.gameObject.name is "TrackEffectTransitionTrimLeft" or "TrackEffectTransitionTrimRight")
                {
                    continue;
                }

                // Change the child's scale such that their world size remains the same after the parent scales
                var originalScale = 0.005f;
                var newScale = originalScale / scaleFactor;
                child.localScale = child.localScale.WithZ(newScale);
                // Adjust the child's position to reflect the new scale
                var signFactor = Math.Sign(child.localPosition.z);
                var newZ = (childZBasePosition + newScale * childZBasePosition) * signFactor;
                // This fudge shouldn't be necessary, but without it there is sometimes
                // a visible gap in the rail between the transition and main section
                // I assume this is because of rounding errors with small float values
                newZ += 0.001f * -signFactor;

                child.localPosition = child.localPosition.WithZ(newZ);
            }
            // With the adjustments to the children made, we can scale the
            // parent and have everything end up in the right place
            cachedTransform.localScale = cachedTransform.localScale.WithZ(zScale);
        }

        // Returns the material corresponding to a specific effect object and effect type
        // NOTE: It is correct that DrumFillAndUnison uses the _drumFillX material for all
        //  parts. This is by request of the artists. (It shouldn't exist, but sometimes happens anyway)
        private Material GetMaterial(string objectName, TrackEffectType effectType)
        {
            var material = objectName switch
            {
                // This first one is not strictly necessary since it should
                // always be a pooled clone, but let's specify just in case
                "TrackEffectTrack" => effectType switch
                {
                    TrackEffectType.Solo              => _soloTrackMaterial,
                    TrackEffectType.Unison            => _unisonTrackMaterial,
                    TrackEffectType.DrumFill          => _drumFillTrackMaterial,
                    TrackEffectType.SoloAndUnison     => _unisonTrackMaterial,
                    TrackEffectType.SoloAndDrumFill   => _drumFillTrackMaterial,
                    TrackEffectType.DrumFillAndUnison => _drumFillTrackMaterial,
                    _                                 => null,
                },
                "TrackEffectTrack(Clone)" => effectType switch
                {
                    TrackEffectType.Solo              => _soloTrackMaterial,
                    TrackEffectType.Unison            => _unisonTrackMaterial,
                    TrackEffectType.DrumFill          => _drumFillTrackMaterial,
                    TrackEffectType.SoloAndUnison     => _unisonTrackMaterial,
                    TrackEffectType.SoloAndDrumFill   => _drumFillTrackMaterial,
                    TrackEffectType.DrumFillAndUnison => _drumFillTrackMaterial,
                    _                                 => null,
                },
                "TrackEffectRailLeft" => effectType switch
                {
                    TrackEffectType.Solo              => _soloRailMaterial,
                    TrackEffectType.Unison            => _unisonRailMaterial,
                    TrackEffectType.DrumFill          => _drumFillRailMaterial,
                    TrackEffectType.SoloAndUnison     => _soloRailMaterial,
                    TrackEffectType.SoloAndDrumFill   => _soloRailMaterial,
                    TrackEffectType.DrumFillAndUnison => _drumFillRailMaterial,
                    _                                 => null,
                },
                "TrackEffectRailRight" => effectType switch
                {
                    TrackEffectType.Solo              => _soloRailMaterial,
                    TrackEffectType.Unison            => _unisonRailMaterial,
                    TrackEffectType.DrumFill          => _drumFillRailMaterial,
                    TrackEffectType.SoloAndUnison     => _soloRailMaterial,
                    TrackEffectType.SoloAndDrumFill   => _soloRailMaterial,
                    TrackEffectType.DrumFillAndUnison => _unisonRailMaterial,
                    _                                 => null
                },
                "TrackEffectTransitionRailLeft" => effectType switch
                {
                    TrackEffectType.Solo              => _soloRailLeftTransitionMaterial,
                    TrackEffectType.Unison            => _unisonRailLeftTransitionMaterial,
                    TrackEffectType.DrumFill          => _drumFillRailLeftTransitionMaterial,
                    TrackEffectType.SoloAndUnison     => _soloRailLeftTransitionMaterial,
                    TrackEffectType.SoloAndDrumFill   => _soloRailLeftTransitionMaterial,
                    TrackEffectType.DrumFillAndUnison => _drumFillRailLeftTransitionMaterial,
                    _                                 => null
                },
                "TrackEffectTransitionRailRight" => effectType switch
                {
                    TrackEffectType.Solo              => _soloRailRightTransitionMaterial,
                    TrackEffectType.Unison            => _unisonRailRightTransitionMaterial,
                    TrackEffectType.DrumFill          => _drumFillRailRightTransitionMaterial,
                    TrackEffectType.SoloAndUnison     => _soloRailRightTransitionMaterial,
                    TrackEffectType.SoloAndDrumFill   => _soloRailRightTransitionMaterial,
                    TrackEffectType.DrumFillAndUnison => _drumFillRailRightTransitionMaterial,
                    _                                 => null
                },
                "TrackEffectTrimLeft" => effectType switch {
                    TrackEffectType.Solo              => _soloTrimMaterial,
                    TrackEffectType.Unison            => _unisonTrimMaterial,
                    TrackEffectType.DrumFill          => _drumFillTrimMaterial,
                    TrackEffectType.SoloAndUnison     => _unisonTrimMaterial,
                    TrackEffectType.SoloAndDrumFill   => _drumFillTrimMaterial,
                    TrackEffectType.DrumFillAndUnison => _drumFillTrimMaterial,
                    _                                 => null,
                },
                "TrackEffectTransitionTrimLeft" => effectType switch
                {
                    TrackEffectType.Solo              => _soloTrimTransitionMaterial,
                    TrackEffectType.Unison            => _unisonTrimTransitionMaterial,
                    TrackEffectType.DrumFill          => _drumFillTrimTransitionMaterial,
                    TrackEffectType.SoloAndUnison     => _unisonTrimTransitionMaterial,
                    TrackEffectType.SoloAndDrumFill   => _drumFillTrimTransitionMaterial,
                    TrackEffectType.DrumFillAndUnison => _drumFillTrimTransitionMaterial,
                    _                                 => null,
                },
                "TrackEffectTrimRight" => effectType switch {
                    TrackEffectType.Solo              => _soloTrimMaterial,
                    TrackEffectType.Unison            => _unisonTrimMaterial,
                    TrackEffectType.DrumFill          => _drumFillTrimMaterial,
                    TrackEffectType.SoloAndUnison     => _unisonTrimMaterial,
                    TrackEffectType.SoloAndDrumFill   => _drumFillTrimMaterial,
                    TrackEffectType.DrumFillAndUnison => _drumFillTrimMaterial,
                    _                                 => null,
                },
                "TrackEffectTransitionTrimRight" => effectType switch
                {
                    TrackEffectType.Solo              => _soloTrimTransitionMaterial,
                    TrackEffectType.Unison            => _unisonTrimTransitionMaterial,
                    TrackEffectType.DrumFill          => _drumFillTrimTransitionMaterial,
                    TrackEffectType.SoloAndUnison     => _unisonTrimTransitionMaterial,
                    TrackEffectType.SoloAndDrumFill   => _drumFillTrimTransitionMaterial,
                    TrackEffectType.DrumFillAndUnison => _drumFillTrimTransitionMaterial,
                    _                                 => null,
                },
                "TrackEffectStart" => effectType switch
                {
                    TrackEffectType.Solo              => _soloTransitionMaterial,
                    TrackEffectType.Unison            => _unisonTransitionMaterial,
                    TrackEffectType.DrumFill          => _drumFillTransitionMaterial,
                    TrackEffectType.SoloAndUnison     => _unisonTransitionMaterial,
                    TrackEffectType.SoloAndDrumFill   => _drumFillTransitionMaterial,
                    TrackEffectType.DrumFillAndUnison => _drumFillTransitionMaterial,
                    _                                 => null
                },
                "TrackEffectEnd" => effectType switch
                {
                    TrackEffectType.Solo              => _soloTransitionMaterial,
                    TrackEffectType.Unison            => _unisonTransitionMaterial,
                    TrackEffectType.DrumFill          => _drumFillTransitionMaterial,
                    TrackEffectType.SoloAndUnison     => _unisonTransitionMaterial,
                    TrackEffectType.SoloAndDrumFill   => _drumFillTransitionMaterial,
                    TrackEffectType.DrumFillAndUnison => _drumFillTransitionMaterial,
                    _                                 => null
                },
                _ => null,
            };

            // null is expected if the material has been set to None
            // in the editor, so we don't check for that here

            return material;
        }

        protected override void HideElement()
        {

        }

        protected override void UpdateElement()
        {
            if (_visibilityInTransition)
            {
                SetAllVisibility(Visibility);
            }

            if (_maskEnabled)
            {
                var timeSinceStart = GameManager.VisualTime - _visibilityStartTime;
                // Go up the track at twice the notespeed (extra parentheses for clarity)
                SetEffectMask((float) (StartZ + (timeSinceStart * Math.Max(10f, Player.NoteSpeed * 3))));
            }
        }

        private float ZFromTime(double time)
        {
            float z = TrackPlayer.STRIKE_LINE_POS + (float) (time - GameManager.VisualTime) * Player.NoteSpeed;
            return z;
        }
    }
}

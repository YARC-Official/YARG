using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Serialization;
using YARG.Core.Logging;
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
        private Material _drumFillTransitionMaterial;
        [SerializeField]
        private Material _drumFillRailLeftTransitionMaterial;
        [SerializeField]
        private Material _drumFillRailRightTransitionMaterial;

        [SerializeField]
        public float Visibility;

        public float StartVisibility;
        public float EndVisibility;

        private bool _visibilityInTransition = false;
        private float _currentVisibility = 1.0f;
        private bool _startVisibilityInTransition = false;
        private bool _endVisibilityInTransition = false;
        private float _currentStartVisibility = 1.0f;
        private float _currentEndVisibility = 1.0f;


        private bool _previousStartTransitionEnable;
        private bool _previousEndTransitionEnable;
        public TrackEffect EffectRef { get; set; }

        private static readonly int _visibility = Shader.PropertyToID("_Visibility");

        public override double ElementTime => EffectRef.Time;
        public double MiddleTime => EffectRef.Time + ((EffectRef.TimeEnd - EffectRef.Time) / 2);
        private float ZLength => (float) (EffectRef.TimeEnd - EffectRef.Time * Player.NoteSpeed);
        // not sure that we really need the +3.5
        protected new float RemovePointOffset => (float) ((EffectRef.TimeEnd - EffectRef.Time) * Player.NoteSpeed + 3.5);

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
            }
        }

        private void SetMaterials()
        {
            var children = GetComponentsInChildren<Renderer>();

            foreach (var child in children)
            {
                if (!child.gameObject.activeInHierarchy)
                {
                    continue;
                }
                var newMaterial = GetMaterial(child.gameObject.name, EffectRef.EffectType);
                if (newMaterial is null)
                {
                    // Games are being played, just disable the object
                    child.gameObject.SetActive(false);
                }

                child.material = newMaterial;
            }
        }

        public void SetStartTransitionVisible(bool enable)
        {
            EnableStartTransition = enable;
            SetTransitionState();
        }

        public void SetEndTransitionVisible(bool enable)
        {
            EnableEndTransition = enable;
            SetTransitionState();
        }

        public void SetTransitionState()
        {
            var cachedTransform = _meshRenderer.transform;

            var children = cachedTransform.GetComponentsInChildren<Transform>(true);

            // TODO: Don't actually disable the gameobject, just set visibility to 0.0f
            //  turns out that doesn't work because of z fighting
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
                        var foo = child.GetComponent<Renderer>();
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
                child.material.SetFloat(_visibility, visibility);
            }
        }

        // TODO: Make the visibility spread up the track so it doesn't just pop in
        //  also fade in the visibility as it does so

        // TODO: Color.Lerp between effect types if they are changing

        // I think what we're looking for here is for it to fade in at the bottom
        // and spread up the track with decreasing transparency

        // For now maybe just spread up the track at full transparency
        public void MakeVisible()
        {
            _visibilityInTransition = true;
            Visibility = 1.0f;
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
                foreach (var material in meshRenderer.materials)
                {
                    material.SetFloat(_visibility, visibility);
                }
            }
            _currentVisibility = visibility;
        }

        protected override bool UpdateElementPosition()
        {
            // Calibration is not taken into consideration here, as that is instead handled in more
            // critical areas such as the game manager and players
            float z =
                TrackPlayer.STRIKE_LINE_POS                          // Shift origin to the strike line
                + (float) (MiddleTime - GameManager.RealVisualTime)  // Get time of center of effect object relative to now
                * Player.NoteSpeed;                                  // Adjust speed (units/s)

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

                if (child.gameObject.name is "TrackEffectRailRight" or "TrackEffectRailLeft")
                {
                    continue;
                }

                if (child.gameObject.name is "TrackEffectTransitionRailLeft" or "TrackEffectTransitionRailRight")
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
                    TrackEffectType.DrumFillAndUnison => _unisonRailMaterial,
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
                    TrackEffectType.DrumFillAndUnison => _unisonRailLeftTransitionMaterial,
                    _                                 => null
                },
                "TrackEffectTransitionRailRight" => effectType switch
                {
                    TrackEffectType.Solo              => _soloRailRightTransitionMaterial,
                    TrackEffectType.Unison            => _unisonRailRightTransitionMaterial,
                    TrackEffectType.DrumFill          => _drumFillRailRightTransitionMaterial,
                    TrackEffectType.SoloAndUnison     => _soloRailRightTransitionMaterial,
                    TrackEffectType.SoloAndDrumFill   => _soloRailRightTransitionMaterial,
                    TrackEffectType.DrumFillAndUnison => _unisonRailRightTransitionMaterial,
                    _                                 => null
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
            if (material is null)
            {
                YargLogger.LogFormatWarning("TrackEffectElement has no material for an object named {0} with effect type {1}",
                    objectName, effectType);
            }
            return material;
        }

        protected override void HideElement()
        {

        }

        protected override void UpdateElement()
        {
            if (_visibilityInTransition)
            {
                SetAllVisibility(Mathf.Lerp(_currentVisibility, Visibility, Time.deltaTime * 5f));
            }
        }
    }
}

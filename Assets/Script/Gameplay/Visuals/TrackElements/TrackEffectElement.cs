using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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

        public TrackPlayer.TrackEffect EffectRef { get; set; }

        private Dictionary<string, Dictionary<TrackPlayer.TrackEffectType, Material>> _materials;

        public override double ElementTime => EffectRef.StartTime;
        public double MiddleTime => EffectRef.StartTime + ((EffectRef.EndTime - EffectRef.StartTime) / 2);
        private float ZLength => (float) (EffectRef.EndTime - EffectRef.StartTime * Player.NoteSpeed);
        // not sure that we really need the +3.5
        protected new float RemovePointOffset => (float) ((EffectRef.EndTime - EffectRef.StartTime) * Player.NoteSpeed + 3.5);

        protected override void InitializeElement()
        {
            InitializeMaterialDict();
            SetTransitionState();
            SetMaterials();
            RescaleForZ();
            InitializeFade();
        }

        private void InitializeMaterialDict()
        {
            // This is absurd
            _materials = new Dictionary<string, Dictionary<TrackPlayer.TrackEffectType, Material>>
            {
                {"TrackEffectTrack",
                    new Dictionary<TrackPlayer.TrackEffectType, Material>
                    {
                        {TrackPlayer.TrackEffectType.Solo, _soloTrackMaterial},
                        {TrackPlayer.TrackEffectType.Unison, _unisonTrackMaterial},
                        {TrackPlayer.TrackEffectType.SoloAndUnison, _unisonTrackMaterial}
                    }
                },
                {"TrackEffectTrack(Clone)",
                    new Dictionary<TrackPlayer.TrackEffectType, Material>
                    {
                        {TrackPlayer.TrackEffectType.Solo, _soloTrackMaterial},
                        {TrackPlayer.TrackEffectType.Unison, _unisonTrackMaterial},
                        {TrackPlayer.TrackEffectType.SoloAndUnison, _unisonTrackMaterial}
                    }
                },
                {"TrackEffectStart",
                    new Dictionary<TrackPlayer.TrackEffectType, Material>
                    {
                        {TrackPlayer.TrackEffectType.Solo, _soloTransitionMaterial},
                        {TrackPlayer.TrackEffectType.Unison, _unisonTransitionMaterial},
                        {TrackPlayer.TrackEffectType.SoloAndUnison, _unisonTransitionMaterial}
                    }
                },
                {"TrackEffectEnd",
                    new Dictionary<TrackPlayer.TrackEffectType, Material>
                    {
                        {TrackPlayer.TrackEffectType.Solo, _soloTransitionMaterial},
                        {TrackPlayer.TrackEffectType.Unison, _unisonTransitionMaterial},
                        {TrackPlayer.TrackEffectType.SoloAndUnison, _unisonTransitionMaterial}
                    }
                },
                {"TrackEffectTransitionRailRight",
                    new Dictionary<TrackPlayer.TrackEffectType, Material>
                    {
                        {TrackPlayer.TrackEffectType.Solo, _soloRailRightTransitionMaterial},
                        {TrackPlayer.TrackEffectType.Unison, _unisonRailRightTransitionMaterial},
                        {TrackPlayer.TrackEffectType.SoloAndUnison, _soloRailRightTransitionMaterial}
                    }
                },
                {"TrackEffectTransitionRailLeft",
                    new Dictionary<TrackPlayer.TrackEffectType, Material>
                    {
                        {TrackPlayer.TrackEffectType.Solo, _soloRailLeftTransitionMaterial},
                        {TrackPlayer.TrackEffectType.Unison, _unisonRailLeftTransitionMaterial},
                        {TrackPlayer.TrackEffectType.SoloAndUnison, _soloRailLeftTransitionMaterial}
                    }
                },
                {"TrackEffectRailRight",
                    new Dictionary<TrackPlayer.TrackEffectType, Material>
                    {
                        {TrackPlayer.TrackEffectType.Solo, _soloRailMaterial},
                        {TrackPlayer.TrackEffectType.Unison, _unisonRailMaterial},
                        {TrackPlayer.TrackEffectType.SoloAndUnison, _soloRailMaterial}
                    }
                },
                {"TrackEffectRailLeft",
                    new Dictionary<TrackPlayer.TrackEffectType, Material>
                    {
                        {TrackPlayer.TrackEffectType.Solo, _soloRailMaterial},
                        {TrackPlayer.TrackEffectType.Unison, _unisonRailMaterial},
                        {TrackPlayer.TrackEffectType.SoloAndUnison, _soloRailMaterial}
                    }
                },
            };
        }

        private void InitializeFade()
        {
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
                child.material = _materials[child.gameObject.name][EffectRef.EffectType];
            }
        }

        private void SetTransitionState()
        {
            EnableStartTransition = EffectRef.StartTransitionEnable;
            EnableEndTransition = EffectRef.EndTransitionEnable;

            var cachedTransform = _meshRenderer.transform;

            var children = cachedTransform.GetComponentsInChildren<Transform>();

            foreach (var child in children)
            {
                if (child == cachedTransform)
                {
                    continue;
                }

                if (child.gameObject.name is "TrackEffectStart" && !EnableStartTransition)
                {
                    child.gameObject.SetActive(false);
                }

                if (child.gameObject.name is "TrackEffectEnd" && !EnableEndTransition)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        protected override bool UpdateElementPosition()
        {
            // Calibration is not taken into consideration here, as that is instead handled in more
            // critical areas such as the game manager and players
            float z =
                TrackPlayer.STRIKE_LINE_POS                          // Shift origin to the strike line
                + (float) (MiddleTime - GameManager.RealVisualTime) // Get time of note relative to now
                * Player.NoteSpeed;                                  // Adjust speed (units/s)

            var cacheTransform = transform;
            cacheTransform.localPosition = cacheTransform.localPosition.WithZ(z);

            if (z < REMOVE_POINT - RemovePointOffset)
            {
                ParentPool.Return(this);
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
            var zScale = (float) (EffectRef.EndTime - EffectRef.StartTime) * Player.NoteSpeed / zSize;

            var cachedTransform = _meshRenderer.transform;

            // A bit of hackery is necessary to avoid the rescaling of the
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
                var originalScale = 0.005f; // this should be child.localScale.z, but that causes issues if the object gets reused
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

        protected override void HideElement()
        {

        }

        protected override void UpdateElement()
        {

        }
    }
}

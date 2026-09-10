using System.Collections.Generic;
using UnityEngine;
using YARG.Gameplay;
using YARG.Helpers;
using YARG.Venue.VenueCamera;
using YARG.Venue.Characters;


namespace YARG.Venue
{
    public class BundleBackgroundManager : MonoBehaviour
    {
        private const string VENUE_LAYER_NAME = "Venue";

        // Awake shoves this object here so its effects don't collide with the
        // tracks. In a yarground bundle everything is one prefab under this
        // root, so it all moves together. A raw venue scene (used as the iOS
        // fallback) has independent roots — the loader must re-align them by
        // the same offset, hence this is public.
        public static readonly Vector3 VenueOffset = Vector3.forward * 10_000f;

        private int _venueLayerNumber = -1;

        // DO NOT CHANGE the name of this! I *know* it doesn't follow naming conventions, but it will also break existing
        // venues if we do change it.
        //
        // ReSharper disable once InconsistentNaming
        [SerializeField]
        private Camera mainCamera;

        [Space]
        [SerializeField]
        private VenueCharacter _replaceableVocalist;

        public AssetBundle       Bundle        { get; set; }
        public List<AssetBundle> ShaderBundles { get; set; } = new();
        public List<AssetBundle> CharacterBundles { get; set; } = new();

        private void Awake()
        {
            // Move object out of the way, so its effects don't collide with the tracks
            transform.position += VenueOffset;
            _venueLayerNumber = LayerMask.NameToLayer(VENUE_LAYER_NAME);
        }

        public void SetupVenueCamera(GameObject bgInstance)
        {
            // If venue has a CameraManager, don't add VenueCameraRenderer, it will be taken care of
            if (bgInstance.GetComponentInChildren<CameraManager>() == null)
            {
                mainCamera.gameObject.AddComponent<VenueCameraRenderer>();
            }
        }

        public void LimitVenueLights(GameObject bgInstance)
        {
            if (_venueLayerNumber == -1)
            {
                return;
            }

            int venueLayer = 1 << _venueLayerNumber;

            Light[] lights = bgInstance.GetComponentsInChildren<Light>(true);

            foreach (var light in lights)
            {
                light.cullingMask = venueLayer;
            }
        }

        private void OnDestroy()
        {
            if (Bundle != null)
            {
                Bundle.Unload(true);
            }

            if (ShaderBundles.Count > 0)
            {
                foreach (var bundle in ShaderBundles)
                {
                    if (bundle != null)
                    {
                        bundle.Unload(true);
                    }
                }

                ShaderBundles.Clear();
            }

            if (CharacterBundles.Count > 0)
            {
                foreach (var bundle in CharacterBundles)
                {
                    if (bundle != null)
                    {
                        bundle.Unload(true);
                    }
                }

                CharacterBundles.Clear();
            }
        }


#if UNITY_EDITOR
        [ContextMenu("Export Vocalist")]
        public void ExportCharacter()
        {
            var vocalist = _replaceableVocalist.gameObject;

            BackgroundHelper.Export(vocalist, BackgroundHelper.ExportType.Character, new string[] {});
        }

        [ContextMenu("Export Background")]
        public void ExportBackground()
        {
            BackgroundHelper.Export(gameObject, BackgroundHelper.ExportType.Background, new string[] {});
        }
#endif
    }
}

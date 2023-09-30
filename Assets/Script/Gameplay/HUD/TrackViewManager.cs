using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using YARG.Player;

namespace YARG.Gameplay.HUD
{
    public class TrackViewManager : GameplayBehaviour
    {
        [Header("Prefabs")]
        [SerializeField]
        private GameObject _trackViewPrefab;

        [Header("References")]
        [SerializeField]
        private RawImage _vocalImage;

        private readonly List<TrackView> _trackViews = new();

        private void Start()
        {
            // Get the aspect ration of the vocal image
            var rect = _vocalImage.rectTransform.ToScreenSpace();
            float ratio = rect.width / rect.height;

            // Apply the vocal track texture
            var rt = GameManager.VocalTrack.InitializeRenderTexture(ratio);
            _vocalImage.texture = rt;
        }

        public TrackView CreateTrackView(TrackPlayer trackPlayer, YargPlayer player)
        {
            // Create a track view
            var trackView = Instantiate(_trackViewPrefab, transform).GetComponent<TrackView>();

            // Set up render texture
            var descriptor = new RenderTextureDescriptor(
                Screen.width, Screen.height,
                RenderTextureFormat.ARGBHalf);
            descriptor.mipCount = 0;
            var renderTexture = new RenderTexture(descriptor);

            // Make the camera render on to the texture instead of the screen
            trackPlayer.TrackCamera.targetTexture = renderTexture;

            // Setup track view to show the correct track
            trackView.Initialize(renderTexture, player.CameraPreset);

            _trackViews.Add(trackView);
            UpdateAllSizing();

            return trackView;
        }

        private void UpdateAllSizing()
        {
            foreach (var trackView in _trackViews)
            {
                trackView.UpdateSizing(_trackViews.Count);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Gameplay.Player;
using YARG.Player;

namespace YARG.Gameplay.HUD
{
    public class TrackViewManager : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField]
        private GameObject _trackViewPrefab;

        private readonly List<TrackView> _trackViews = new();

        public TrackView CreateTrackView(BasePlayer basePlayer, YargPlayer player)
        {
            // Create a track view
            var trackView = Instantiate(_trackViewPrefab, transform).GetComponent<TrackView>();

            // Set up render texture
            var descriptor = new RenderTextureDescriptor(
                Screen.width, Screen.height,
                RenderTextureFormat.ARGBHalf
            );
            descriptor.mipCount = 0;
            var renderTexture = new RenderTexture(descriptor);

            // Make the camera render on to the texture instead of the screen
            basePlayer.TrackCamera.targetTexture = renderTexture;

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
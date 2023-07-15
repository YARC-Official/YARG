using System.Collections.Generic;
using UnityEngine;

namespace YARG.Gameplay.HUD
{
    public class TrackViewManager : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField]
        private GameObject _trackViewPrefab;

        private readonly List<TrackView> _trackViews = new();

        public void CreateTrackView(BasePlayer player)
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

            // Set render target
            player.TrackCamera.targetTexture = renderTexture;
            trackView.TrackImage.texture = renderTexture;

            UpdateAllSizing();
            _trackViews.Add(trackView);
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
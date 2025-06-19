using System.Collections.Generic;
using UnityEngine;
using YARG.Gameplay.Player;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Player;

namespace YARG.Gameplay.HUD
{
    public class TrackViewManager : GameplayBehaviour
    {
        [Header("Prefabs")]
        [SerializeField]
        private GameObject _trackViewPrefab;
        [SerializeField]
        private GameObject _vocalHudPrefab;
        [SerializeField]
        private HighwayCameraRendering _highwayCameraRendering;

        [Header("References")]
        [SerializeField]
        private RectTransform _vocalImage;
        [SerializeField]
        private Transform _vocalHudParent;
        [SerializeField]
        private CountdownDisplay _vocalsCountdownDisplay;

        private RenderTexture _highwaysOutputTexture;
        private readonly List<TrackView> _trackViews = new();

        public TrackView CreateTrackView(TrackPlayer trackPlayer, YargPlayer player)
        {
            // Create a track view
            var trackView = Instantiate(_trackViewPrefab, transform).GetComponent<TrackView>();

            // Setup track view to show the correct track
            trackView.Initialize(trackPlayer);

            trackPlayer.TrackCamera.targetTexture  = _highwayCameraRendering.GetHighwayOutputTexture();
            _highwayCameraRendering.AddTrackPlayer(trackPlayer, player);

            _trackViews.Add(trackView);
            UpdateAllSizing();

            return trackView;
        }

        public void CreateVocalTrackView()
        {
            _vocalImage.gameObject.SetActive(true);

            // Get the aspect ratio of the vocal image
            var rect = _vocalImage.ToScreenSpace();
            float ratio = rect.width / rect.height;

            // Apply the vocal track texture
            GameManager.VocalTrack.InitializeRenderTexture(ratio, _highwayCameraRendering.GetHighwayOutputTexture());
        }

        public VocalsPlayerHUD CreateVocalsPlayerHUD()
        {
            var go = Instantiate(_vocalHudPrefab, _vocalHudParent);
            return go.GetComponent<VocalsPlayerHUD>();
        }

        private void UpdateAllSizing()
        {
            int count = _trackViews.Count;
            for (int i = 0; i < count; ++i)
            {
                _trackViews[i].UpdateSizing(count, i);
            }
        }

        public void SetAllHUDPositions()
        {
            // The positions of the track view have probably not updated yet at this point
            Canvas.ForceUpdateCanvases();

            foreach (var view in _trackViews)
                view.UpdateHUDPosition();
        }
    }
}

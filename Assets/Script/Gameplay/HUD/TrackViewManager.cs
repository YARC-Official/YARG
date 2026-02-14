using System.Collections.Generic;
using UnityEngine;
using YARG.Gameplay.Player;
using YARG.Gameplay.Visuals;

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

        private readonly List<TrackView> _trackViews = new();

        public TrackView CreateTrackView()
        {
            // Create a track view
            var trackView = Instantiate(_trackViewPrefab, transform).GetComponent<TrackView>();
            trackView.Initialize(_highwayCameraRendering);
            _trackViews.Add(trackView);
            return trackView;
        }

        private bool isInit = false;

        protected override void GameplayAwake()
        {
            _highwayCameraRendering.OnHighwaysTextureCreated += OnTextureCreated;
        }

        private void OnTextureCreated(RenderTexture obj)
        {
            InitializeRenderTexture(_highwayCameraRendering.HighwaysOutputTexture);
        }

        public void CreateVocalTrackView()
        {
            _vocalImage.gameObject.SetActive(true);
        }

        private void InitializeRenderTexture(RenderTexture texture)
        {
            GameManager.VocalTrack.InitializeRenderTexture(_vocalImage, texture);
        }

        public VocalsPlayerHUD CreateVocalsPlayerHUD()
        {
            var go = Instantiate(_vocalHudPrefab, _vocalHudParent);
            return go.GetComponent<VocalsPlayerHUD>();
        }

        public void AddTrackPlayer(TrackPlayer trackPlayer)
        {
            _highwayCameraRendering.AddTrackPlayer(trackPlayer);
        }

        protected override void GameplayDestroy()
        {
            _highwayCameraRendering.OnHighwaysTextureCreated -= OnTextureCreated;
        }
    }
}
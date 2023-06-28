using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using YARG.PlayMode;
using YARG.Util;

namespace YARG.UI
{
    public class GameUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject trackView;

        [Space]
        [SerializeField]
        private Transform trackContainer;

        [SerializeField]
        private RawImage vocalTrack;

        [SerializeField]
        private TextMeshProUGUI loadingText;

        public GameObject loadingContainer;
        public GameObject pauseMenu;
        public RawImage background;
        public VideoPlayer videoPlayer;
        public static GameUI Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        public void AddTrackImage(RenderTexture rt, CommonTrack commonTrack)
        {
            var trackImage = Instantiate(trackView, trackContainer);

            var view = trackImage.GetComponent<TrackView>();
            view.TrackImage.texture = rt;

            commonTrack.TrackView = view;

            UpdateAllSizing();
        }

        public void SetVocalTrackImage(RenderTexture rt)
        {
            vocalTrack.texture = rt;

            // TODO: Whyyy. figure out a better way to scale.
            var rect = vocalTrack.rectTransform.ToViewportSpace();
            vocalTrack.uvRect = new(0f, rect.y / 1.9f, rect.width, rect.height);
        }

        public void RemoveVocalTrackImage()
        {
            Destroy(vocalTrack.gameObject);
        }

        private void UpdateAllSizing()
        {
            foreach (var trackView in trackContainer.GetComponentsInChildren<TrackView>())
            {
                trackView.UpdateSizing(trackContainer.childCount);
            }
        }

        public void SetLoadingText(string str)
        {
            loadingText.text = str;
        }
    }
}
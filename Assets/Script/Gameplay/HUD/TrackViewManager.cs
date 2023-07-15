using UnityEngine;

namespace YARG.Gameplay.HUD
{
    public class TrackViewManager : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField]
        private GameObject _trackViewPrefab;

        public void CreateTrackView(BasePlayer basePlayer)
        {
            var trackView = Instantiate(_trackViewPrefab, transform);
        }
    }
}
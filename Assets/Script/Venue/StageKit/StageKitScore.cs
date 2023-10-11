using UnityEngine;

namespace YARG
{
    public class StageKitScore : MonoBehaviour
    {
        private StageKitLightingController _controller;

        private void Awake()
        {
            _controller = StageKitLightingController.Instance;
            _controller.StageKits.ForEach(kit => kit.ResetHaptics());
            _controller.CurrentLightingCue = new ScoreLighting();
        }

        private void OnDestroy()
        {
            _controller.CurrentLightingCue.Dispose(true);
        }
    }
}
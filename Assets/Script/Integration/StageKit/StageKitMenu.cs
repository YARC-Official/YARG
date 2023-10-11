using UnityEngine;

namespace YARG
{
    public class StageKitMenu : MonoBehaviour
    {
        private StageKitLightingController _controller;

        private void Awake()
        {
            _controller = StageKitLightingController.Instance;
            _controller.StageKits.ForEach(kit => kit.ResetHaptics());
            _controller.CurrentLightingCue = new MenuLighting(); //turn on the menu lighting cue, custom made for YARG!
        }

        private void OnDestroy()
        {
            _controller.CurrentLightingCue.Dispose(true);
        }
    }
}

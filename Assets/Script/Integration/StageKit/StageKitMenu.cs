using UnityEngine;

namespace YARG.Integration.StageKit
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
            KillCue();
        }

        private void OnApplicationQuit()
        {
            KillCue();
        }

        private void KillCue() //The only cue used on the menu screen is timed, no need to have all the action and token stuff here.
        {
            foreach (var primitive in _controller.CurrentLightingCue.CuePrimitives)
            {
                primitive.CancellationTokenSource.Cancel();
            }

            _controller.CuePrimitives.Clear();
            _controller.PreviousLightingCue = _controller.CurrentLightingCue;
            _controller.CurrentLightingCue = null;
            _controller.StageKits.ForEach(kit => kit.ResetHaptics());
        }
    }
}
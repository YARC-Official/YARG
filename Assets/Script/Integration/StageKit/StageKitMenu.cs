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
            //turn on the menu lighting cue, custom made for YARG!
            _controller.CurrentLightingCue = new MenuLighting();
        }

        private void OnDestroy()
        {
            KillCue();
        }

        private void OnApplicationQuit()
        {
            KillCue();
        }

        //The only cue used on the menu screen is timed, no need to have all the action and token stuff here.
        private void KillCue()
        {
            if(_controller.CurrentLightingCue == null) return;
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
using UnityEngine;
using YARG.Core.Game;
using YARG.Core.Input;

namespace YARG.Gameplay.HUD
{
    public abstract class BaseInputViewer : MonoBehaviour
    {

        [SerializeField]
        protected InputViewerButton[] _buttons;

        public abstract void OnInput(GameInput input);

        public abstract void SetColors(ColorProfile colorProfile);

        public void ResetButtons()
        {
            foreach(var button in _buttons)
            {
                button.ResetState();
            }
        }
    }
}

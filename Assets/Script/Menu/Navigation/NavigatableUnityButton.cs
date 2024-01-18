using UnityEngine;
using UnityEngine.UI;

namespace YARG.Menu.Navigation
{
    [RequireComponent(typeof(Button))]
    public class NavigatableUnityButton : NavigatableBehaviour
    {
        private Button _button;

        protected override void Awake()
        {
            base.Awake();

            _button = GetComponent<Button>();
        }

        public override void Confirm()
        {
            if (!_button.interactable) return;

            _button.onClick.Invoke();
        }
    }
}
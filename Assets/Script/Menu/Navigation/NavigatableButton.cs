using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YARG.Menu.Navigation
{
    public sealed class NavigatableButton : NavigatableBehaviour, IPointerEnterHandler, INavigationConfirmable
    {
        [SerializeField]
        private Button.ButtonClickedEvent _onClick = new();

        public void OnPointerEnter(PointerEventData eventData)
        {
            Selected = true;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            Confirm();
        }

        public void Confirm()
        {
            _onClick.Invoke();
        }
    }
}
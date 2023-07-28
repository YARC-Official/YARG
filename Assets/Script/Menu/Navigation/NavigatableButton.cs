using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YARG.Menu.Navigation
{
    public sealed class NavigatableButton : NavigatableBehaviour, IPointerEnterHandler
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
            _onClick.Invoke();
        }
    }
}
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YARG.Menu.Navigation
{
    public sealed class NavigatableButton : NavigatableBehaviour
    {
        [SerializeField]
        private Button.ButtonClickedEvent _onClick = new();
        private bool eatNextClick;

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (!eatNextClick)
            {
                Confirm();
            }
            else
            {
                eatNextClick = false;
            }
        }

        public override void Confirm()
        {
            _onClick.Invoke();
        }

        public void RemoveOnClickListeners()
        {
            _onClick.RemoveAllListeners();
        }

        public void SetOnClickEvent(UnityAction a)
        {
            _onClick.RemoveAllListeners();
            _onClick.AddListener(a);
        }
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                eatNextClick = true;
            }
            else
            {
                Invoke("clearEatNextClick", 0.05f);
            }
        }
        private void clearEatNextClick()
        {
            eatNextClick = false;
        }
    }
}
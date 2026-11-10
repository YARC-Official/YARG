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

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            Confirm();
        }

        public override void Confirm()
        {
            _onClick.Invoke();
        }

        /// <summary>True if this button's click event calls a method with the given name (as set in the inspector).</summary>
        public bool CallsMethod(string methodName)
        {
            for (int i = 0; i < _onClick.GetPersistentEventCount(); i++)
            {
                if (_onClick.GetPersistentMethodName(i) == methodName)
                {
                    return true;
                }
            }
            return false;
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
    }
}
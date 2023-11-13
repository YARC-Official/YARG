using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YARG.Menu
{
    public class DragSlider : Slider
    {

        public UnityEvent<float> OnSliderDrag;

        public override void OnDrag(PointerEventData eventData)
        {
            // Have to recreate this function because WHY did they make it private????
            if (!MayDrag(eventData))
            {
                return;
            }

            // Slider is draggable

            base.OnDrag(eventData);

            OnSliderDrag?.Invoke(value);
        }

        private bool MayDrag(PointerEventData eventData)
        {
            return IsActive() && IsInteractable() && eventData.button == PointerEventData.InputButton.Left;
        }
    }
}
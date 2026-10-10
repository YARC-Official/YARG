using UnityEngine;
using UnityEngine.EventSystems;

namespace YARG.Menu.Settings
{
    public sealed class SettingsControlFocus : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerDownHandler
    {
        public SettingsItemVisual Item { private get; set; }

        public void OnSelect(BaseEventData eventData)
        {
            Item.FocusControl(transform);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            Item.ReleaseControl();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Item.FocusControl(transform);
        }
    }
}

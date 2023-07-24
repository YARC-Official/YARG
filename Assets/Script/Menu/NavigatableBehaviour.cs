using UnityEngine;
using UnityEngine.EventSystems;

namespace YARG.Menu
{
    public abstract class NavigatableBehaviour : MonoBehaviour, IPointerDownHandler
    {
        public NavigationGroup NavigationGroup { get; set; }

        private bool _selected;
        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;

                if (value)
                {
                    NavigationGroup.DeselectAll();
                }

                _selected = value;
                OnSelectionChanged(value);
            }
        }

        protected abstract void OnSelectionChanged(bool selected);

        public void OnPointerDown(PointerEventData eventData)
        {
            Selected = true;
        }
    }
}
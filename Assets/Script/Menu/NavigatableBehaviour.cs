using UnityEngine;
using UnityEngine.EventSystems;

namespace YARG.Menu
{
    public abstract class NavigatableBehaviour : MonoBehaviour, IPointerClickHandler
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

        public void OnPointerClick(PointerEventData eventData)
        {
            Selected = true;
        }
    }
}
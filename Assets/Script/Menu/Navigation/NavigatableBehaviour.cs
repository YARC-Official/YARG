using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace YARG.Menu.Navigation
{
    public abstract class NavigatableBehaviour : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        [SerializeField]
        private bool _selectOnHover = false;

        [Space]
        [SerializeField]
        private GameObject _selectedVisual;

        public NavigationGroup NavigationGroup { get; set; }

        private bool _selected = false;
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

                // Make sure these happen after, because they call events that rely on the above.
                if (value)
                {
                    NavigationGroup.SelectedBehaviour = this;
                    NavigationGroup.CurrentNavigationGroup = NavigationGroup;
                }
            }
        }

        protected virtual void Awake()
        {
            // We use _selected here to avoid the selected visual not showing up when the navigation group has
            // initialized, enabled, and set this as selected before this has awoken
            _selectedVisual.SetActive(_selected);
        }

        protected virtual void OnSelectionChanged(bool selected)
        {
            _selectedVisual.SetActive(selected);
        }

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            if (_selectOnHover)
                Selected = true;
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            Selected = true;
        }

        public virtual void Confirm()
        {
        }
    }
}
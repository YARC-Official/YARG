using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace YARG.Menu.Navigation
{
    public abstract class NavigatableBehaviour : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        [SerializeField]
        private bool _selectOnHover;

        [Space]
        [SerializeField]
        private GameObject _selectedVisual;

        public NavigationGroup NavigationGroup { get; set; }

        private bool _selected;
        public bool Selected
        {
            get => _selected;
            private set
            {
                _selected = value;
                SelectionStateChanged?.Invoke(value);
            }
        }

        public event Action<bool> SelectionStateChanged;

        protected virtual void Awake()
        {
            // We use _selected here to avoid the selected visual not showing up when the navigation group has
            // initialized, enabled, and set this as selected before this has awoken
            _selectedVisual.SetActive(Selected);
        }

        public void SetSelected(bool selected, SelectionOrigin selectionOrigin)
        {
            if (Selected == selected) return;

            if (selected)
            {
                NavigationGroup.DeselectAll();
            }

            Selected = selected;
            OnSelectionChanged(selected);

            // Make sure these happen after, because they call events that rely on the above.
            if (selected)
            {
                NavigationGroup.SetSelected(this, selectionOrigin);
                NavigationGroup.SetAsCurrent();
            }
        }

        protected virtual void OnSelectionChanged(bool selected)
        {
            _selectedVisual.SetActive(selected);
        }

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            if (_selectOnHover)
            {
                SetSelected(true, SelectionOrigin.Mouse);
            }
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            SetSelected(true, SelectionOrigin.Mouse);
        }

        public virtual void Confirm()
        {
        }
    }
}
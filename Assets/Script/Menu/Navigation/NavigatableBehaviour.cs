using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace YARG.Menu.Navigation
{
    public abstract class NavigatableBehaviour : MonoBehaviour, IPointerMoveHandler, IPointerDownHandler
    {
        [SerializeField]
        private bool _selectOnHover;

        [Space]
        [SerializeField]
        private GameObject _selectedVisual;

        public NavigationGroup NavigationGroup { get; set; }

        public bool Selected { get; private set; }

        public event Action<NavigatableBehaviour, bool, SelectionOrigin> SelectionStateChanged;

        protected virtual void Awake()
        {
            _selectedVisual.SetActive(Selected);
        }

        protected virtual void OnDestroy()
        {
            SetSelected(false, SelectionOrigin.Programmatically);

            if (NavigationGroup != null)
                NavigationGroup.RemoveNavigatable(this);
        }

        public void SetSelected(bool selected, SelectionOrigin selectionOrigin)
        {
            if (Selected == selected) return;

            Selected = selected;
            OnSelectionChanged(selected);
            SelectionStateChanged?.Invoke(this, selected, selectionOrigin);
        }

        protected virtual void OnSelectionChanged(bool selected)
        {
            _selectedVisual.SetActive(selected);
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            SetSelected(true, SelectionOrigin.Mouse);
        }

        public virtual void Confirm()
        {
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (_selectOnHover)
            {
                SetSelected(true, SelectionOrigin.Mouse);
            }
        }
    }
}
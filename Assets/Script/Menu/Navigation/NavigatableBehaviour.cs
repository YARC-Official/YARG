using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace YARG.Menu.Navigation
{
    public abstract class NavigatableBehaviour : MonoBehaviour, IPointerMoveHandler, IPointerDownHandler,
        IPointerClickHandler
    {
        [SerializeField]
        private bool _selectOnHover;
        [SerializeField]
        private bool _selectOnClick = true;

        [Space]
        [SerializeField]
        private GameObject _selectedVisual;

        public NavigationGroup NavigationGroup { get; set; }

        public bool Selected { get; private set; }

        /// <summary>
        /// Whether moving the pointer over this element selects it. Disable this
        /// for rows that never join a navigation group (like menu titles), so the
        /// pointer cannot highlight them. Selection driven by the navigation group
        /// (keyboard/controller) is unaffected.
        /// </summary>
        public bool SelectOnHover
        {
            get => _selectOnHover;
            set => _selectOnHover = value;
        }

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

        public void OnPointerDown(PointerEventData eventData)
        {
            // A finger going down is also how every scroll gesture starts, so
            // touch acts on release instead: uGUI drops the click once a drag
            // begins, and a finger that never dragged clicks what it pressed
            if (!IsTouch(eventData))
            {
                OnPress();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsTouch(eventData))
            {
                OnPress();
            }
        }

        /// <summary>
        /// The pointer activated this navigatable: on press for a mouse, on
        /// release for a touch.
        /// </summary>
        protected virtual void OnPress()
        {
            if (_selectOnClick)
            {
                SetSelected(true, SelectionOrigin.Mouse);
            }
        }

        public static bool IsTouch(PointerEventData eventData)
        {
            if (eventData is ExtendedPointerEventData extended)
            {
                return extended.pointerType == UIPointerType.Touch;
            }

            // The legacy input module gives touches their finger id and mice negative ids
            return eventData.pointerId >= 0;
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
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace YARG.Menu.Navigation
{
    public abstract class NavigatableBehaviour : MonoBehaviour, IPointerDownHandler
    {
        public NavigationGroup NavigationGroup { get; set; }

        public event Action<bool> SelectionChanged;

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
                    NavigationGroup.CurrentNavigationGroup = NavigationGroup;
                }

                _selected = value;

                // Call events
                SelectionChanged?.Invoke(value);
                OnSelectionChanged(value);
            }
        }

        protected virtual void OnSelectionChanged(bool selected)
        {
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            Selected = true;
        }
    }
}
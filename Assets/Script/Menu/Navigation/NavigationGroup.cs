using System;
using System.Collections.Generic;
using UnityEngine;

namespace YARG.Menu.Navigation
{
    public sealed class NavigationGroup : MonoBehaviour
    {
        public static NavigationGroup CurrentNavigationGroup;

        private readonly List<NavigatableBehaviour> _navigatables = new();

        [SerializeField]
        private bool _defaultGroup;

        [SerializeField]
        private bool _addAllChildrenOnAwake;

        [SerializeField]
        private bool _selectFirst;

        private NavigatableBehaviour _selectedBehaviour;
        public NavigatableBehaviour SelectedBehaviour
        {
            get => _selectedBehaviour;
            set
            {
                _selectedBehaviour = value;
                SelectionChanged?.Invoke(value);
            }
        }

        public int SelectedIndex => _navigatables.IndexOf(SelectedBehaviour);

        public event Action<NavigatableBehaviour> SelectionChanged;

        private void Awake()
        {
            if (_addAllChildrenOnAwake)
            {
                foreach (var navigatable in GetComponentsInChildren<NavigatableBehaviour>())
                {
                    navigatable.NavigationGroup = this;
                    _navigatables.Add(navigatable);
                }
            }
        }

        private void OnEnable()
        {
            if (_defaultGroup)
            {
                CurrentNavigationGroup = this;
            }

            if (_selectFirst && SelectedIndex < 0)
            {
                SelectFirst();
            }
        }

        public void AddNavigatable(NavigatableBehaviour n)
        {
            _navigatables.Add(n);
            n.NavigationGroup = this;
        }

        public void AddNavigatable(GameObject n)
        {
            AddNavigatable(n.GetComponent<NavigatableBehaviour>());
        }

        public void RemoveNavigatable(NavigatableBehaviour n)
        {
            _navigatables.Remove(n);
        }

        public void ClearNavigatables()
        {
            _navigatables.Clear();
        }

        public void DeselectAll()
        {
            foreach (var navigatable in _navigatables)
            {
                navigatable.Selected = false;
            }
        }

        public void SelectFirst()
        {
            if (_navigatables.Count < 1) return;

            _navigatables[0].Selected = true;
        }

        public void SelectNext(NavigationContext context)
        {
            int selected = SelectedIndex;
            if (selected < 0) return;

            selected++;
            if (selected >= _navigatables.Count)
            {
                // Stop at group bounds on repeated inputs
                if (context.IsRepeat)
                    return;

                selected = 0;
            }

            _navigatables[selected].Selected = true;
        }

        public void SelectPrevious(NavigationContext context)
        {
            int selected = SelectedIndex;
            if (selected < 0) return;

            selected--;
            if (selected < 0)
            {
                // Stop at group bounds on repeated inputs
                if (context.IsRepeat)
                    return;

                selected = _navigatables.Count - 1;
            }

            _navigatables[selected].Selected = true;
        }

        public void ConfirmSelection()
        {
            _navigatables[SelectedIndex].Confirm();
        }
    }
}
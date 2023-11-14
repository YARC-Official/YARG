using System.Collections.Generic;
using UnityEngine;

namespace YARG.Menu.Navigation
{
    public sealed class NavigationGroup : MonoBehaviour
    {
        public static NavigationGroup CurrentNavigationGroup { get; private set;  }

        private readonly List<NavigatableBehaviour> _navigatables = new();

        [SerializeField]
        private bool _defaultGroup;
        [SerializeField]
        private bool _canBeCurrent = true;

        [Space]
        [SerializeField]
        private bool _addAllChildrenOnAwake;
        [SerializeField]
        private bool _selectFirst;

        public NavigatableBehaviour SelectedBehaviour { get; private set; }

        public int SelectedIndex => _navigatables.IndexOf(SelectedBehaviour);

        public delegate void SelectionAction(NavigatableBehaviour selected, SelectionOrigin selectionOrigin);
        public event SelectionAction SelectionChanged;

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
                navigatable.SetSelected(false, SelectionOrigin.Programmatically);
            }
        }

        public void SetSelected(NavigatableBehaviour navigatableBehaviour, SelectionOrigin selectionOrigin)
        {
            SelectedBehaviour = navigatableBehaviour;
            SelectionChanged?.Invoke(navigatableBehaviour, selectionOrigin);
        }

        public void SelectFirst()
        {
            if (_navigatables.Count < 1) return;

            _navigatables[0].SetSelected(true, SelectionOrigin.Programmatically);
        }

        public void SelectNext()
        {
            int selected = SelectedIndex;
            if (selected < 0) return;

            selected++;

            // DON'T loop the value
            if (selected >= _navigatables.Count)
            {
                return;
            }

            _navigatables[selected].SetSelected(true, SelectionOrigin.Navigation);
        }

        public void SelectPrevious()
        {
            int selected = SelectedIndex;
            if (selected < 0) return;

            selected--;

            // DON'T loop the value
            if (selected < 0)
            {
                return;
            }

            _navigatables[selected].SetSelected(true, SelectionOrigin.Navigation);
        }

        public void ConfirmSelection()
        {
            _navigatables[SelectedIndex].Confirm();
        }

        public void SetAsCurrent()
        {
            if (_canBeCurrent)
            {
                CurrentNavigationGroup = this;
            }
        }
    }
}
﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace YARG.Menu.Navigation
{
    public sealed class NavigationGroup : MonoBehaviour
    {
        public static NavigationGroup CurrentNavigationGroup { get; private set; }

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

        public int Count => _navigatables.Count;

        public int SelectedIndex { get; private set; }

        public NavigatableBehaviour SelectedBehaviour
        {
            get
            {
                if (_navigatables.Count < 1 || SelectedIndex < 0)
                    return null;

                // Ensure selected index stays within bounds
                SelectedIndex = Math.Clamp(SelectedIndex, 0, _navigatables.Count - 1);
                return _navigatables[SelectedIndex];
            }
        }

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

        public void AddNavigatable(NavigatableBehaviour navigatable)
        {
            _navigatables.Add(navigatable);
            navigatable.NavigationGroup = this;
        }

        public void AddNavigatable(GameObject gameObj)
        {
            if (!gameObj.TryGetComponent<NavigatableBehaviour>(out var navigatable))
                return;

            AddNavigatable(navigatable);
        }

        public void RemoveNavigatable(NavigatableBehaviour navigatable)
        {
            if (SelectedBehaviour == navigatable)
                DeselectCurrent();

            _navigatables.Remove(navigatable);
        }

        public void ClearNavigatables()
        {
            _navigatables.Clear();
        }

        public void SelectFirst()
        {
            SelectAt(0);
        }

        public void SelectLast()
        {
            SelectAt(_navigatables.Count - 1);
        }

        public void SelectNext()
        {
            int selected = SelectedIndex;
            if (selected < 0 || selected >= _navigatables.Count - 1)
                return;

            SelectAt(selected + 1, SelectionOrigin.Navigation);
        }

        public void SelectPrevious()
        {
            int selected = SelectedIndex;
            if (selected <= 0)
                return;

            SelectAt(selected - 1, SelectionOrigin.Navigation);
        }


        public void SelectAt(int index, SelectionOrigin selectionOrigin = SelectionOrigin.Programmatically)
        {
            if (index >= _navigatables.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be less than the count of navigatables ({_navigatables.Count})!");

            if (index == SelectedIndex)
                return;

            if (SelectedBehaviour != null)
                SelectedBehaviour.SetSelected(false, selectionOrigin);

            SelectedIndex = index;
            if (SelectedBehaviour != null)
                SelectedBehaviour.SetSelected(true, selectionOrigin);

            SelectionChanged?.Invoke(SelectedBehaviour, selectionOrigin);
        }

        // Called from NavigatableBehaviours when they get selected
        public void SetSelectedFromNavigatable(NavigatableBehaviour navigatableBehaviour, SelectionOrigin selectionOrigin)
        {
            int index;
            if (navigatableBehaviour != null)
            {
                index = _navigatables.IndexOf(navigatableBehaviour);
                if (index < 0)
                    throw new ArgumentException("The navigation item being selected is not present in the list!");
            }
            else
            {
                index = -1;
            }

            if (index == SelectedIndex)
                return;

            if (SelectedBehaviour != null)
                SelectedBehaviour.SetSelected(false, selectionOrigin);

            SelectedIndex = index;
            SelectionChanged?.Invoke(SelectedBehaviour, selectionOrigin);
        }

        public void DeselectCurrent()
        {
            if (SelectedBehaviour != null)
                SelectedBehaviour.SetSelected(false, SelectionOrigin.Programmatically);

            SelectedIndex = -1;
            SelectionChanged?.Invoke(null, SelectionOrigin.Programmatically);
        }

        public void DeselectAll()
        {
            foreach (var navigatable in _navigatables)
            {
                navigatable.SetSelected(false, SelectionOrigin.Programmatically);
            }

            SelectedIndex = -1;
            SelectionChanged?.Invoke(null, SelectionOrigin.Programmatically);
        }

        public void ConfirmSelection()
        {
            if (SelectedBehaviour != null)
                SelectedBehaviour.Confirm();
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
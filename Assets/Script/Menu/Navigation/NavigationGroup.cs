using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Menu.Navigation
{
    public sealed class NavigationGroup : MonoBehaviour
    {
        private static readonly List<NavigationGroup> _navGroupsStack = new();

        public static NavigationGroup CurrentNavigationGroup => _navGroupsStack.Count <= 0
            ? null
            : _navGroupsStack[^1];

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

        public int? SelectedIndex { get; private set; } = null;

        public NavigatableBehaviour SelectedBehaviour
        {
            get
            {
                if (SelectedIndex is not {} index || index < 0 || index >= _navigatables.Count)
                    return null;

                return _navigatables[index];
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
                    _AddNavigatable(navigatable);
                }
            }
        }

        private void OnEnable()
        {
            if (_defaultGroup)
            {
                _navGroupsStack.Add(this);
            }

            if (_selectFirst && SelectedBehaviour == null)
            {
                SelectFirst();
            }
        }

        private void OnDisable()
        {
            // Remove this navigation group from the stack
            if (_navGroupsStack.Contains(this))
            {
                _navGroupsStack.Remove(this);
            }
        }

        private void _AddNavigatable(NavigatableBehaviour navigatable)
        {
            if (_navigatables.Contains(navigatable))
                throw new InvalidOperationException($"Navigation group {this} already contains navigatable {navigatable}!");

            _navigatables.Add(navigatable);
            navigatable.NavigationGroup = this;
            navigatable.SelectionStateChanged += OnSelectionStateChanged;
        }

        public void AddNavigatable(NavigatableBehaviour navigatable)
        {
            if (_addAllChildrenOnAwake)
            {
                YargLogger.LogFormatWarning("Navigation group {0} has 'Add All Children On Awake' enabled but is being added " +
                    "to manually! This is most likely an error and will result in duplicate entries, so it has been disabled.",
                    ToString());
                _addAllChildrenOnAwake = false;
            }

            _AddNavigatable(navigatable);
        }

        public void AddNavigatable(GameObject gameObj)
        {
            if (!gameObj.TryGetComponent<NavigatableBehaviour>(out var navigatable))
                return;

            AddNavigatable(navigatable);
        }

        public void RemoveNavigatable(NavigatableBehaviour navigatable)
        {
            if (SelectedBehaviour == navigatable && SelectedBehaviour != null)
                SelectedBehaviour.SetSelected(false, SelectionOrigin.Programmatically);

            _navigatables.Remove(navigatable);
            navigatable.NavigationGroup = null;
        }

        public void ClearNavigatables()
        {
            ClearSelection();
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

        public void SelectNext(bool isHeld = false)
        {
            // Allows the user to quickly select an option without needing mouse
            if (SelectedIndex is null)
            {
                SelectFirst();
                return;
            }

            // If the selection will go out of range...
            if (SelectedIndex is not { } selected || selected < 0 || selected >= _navigatables.Count - 1)
            {
                if (!isHeld && SettingsManager.Settings.WrapAroundNavigation.Value)
                {
                    SelectAt(0, SelectionOrigin.Navigation);
                }
                return;
            }

            SelectAt(selected + 1, SelectionOrigin.Navigation);
        }

        public void SelectPrevious(bool isHeld = false)
        {
            // Allows the user to quickly select an option without needing mouse
            if (SelectedIndex is null)
            {
                SelectLast();
                return;
            }

            // If the selection is invalid...
            if (SelectedIndex is not { } selected || selected <= 0)
            {
                if (!isHeld && SettingsManager.Settings.WrapAroundNavigation.Value)
                {
                    SelectAt(_navigatables.Count - 1, SelectionOrigin.Navigation);
                }
                return;
            }

            SelectAt(selected - 1, SelectionOrigin.Navigation);
        }

        public void SelectAt(int? index, SelectionOrigin selectionOrigin = SelectionOrigin.Programmatically)
        {
            if (index == SelectedIndex || _navigatables.Count < 1)
                return;

            if (index < 0 || index >= _navigatables.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between 0 and the count of navigatables ({_navigatables.Count})!");

            if (SelectedBehaviour != null)
                SelectedBehaviour.SetSelected(false, selectionOrigin);

            SelectedIndex = index;
            if (SelectedBehaviour != null)
                SelectedBehaviour.SetSelected(true, selectionOrigin);

            SelectionChanged?.Invoke(SelectedBehaviour, selectionOrigin);
        }

        private void OnSelectionStateChanged(NavigatableBehaviour navigatableBehaviour, bool selected,
            SelectionOrigin selectionOrigin)
        {
            int? index;
            if (selected)
            {
                if (SelectedBehaviour == navigatableBehaviour)
                    return;

                index = _navigatables.IndexOf(navigatableBehaviour);
                if (index < 0)
                    throw new ArgumentException("The navigation item being selected is not present in the list!");
            }
            else
            {
                if (SelectedBehaviour != navigatableBehaviour)
                    return;

                index = null;
            }

            // Avoid a redundant runthrough of this code by deselecting the previous before
            // setting the index, so that the SelectedBehaviour != navigatableBehaviour check above happens
            var previousSelection = SelectedBehaviour;
            SelectedIndex = index;
            if (previousSelection != null)
                previousSelection.SetSelected(false, selectionOrigin);

            SelectionChanged?.Invoke(SelectedBehaviour, selectionOrigin);
            if (selected)
            {
                PushNavGroupToStack();
            }
        }

        public void ClearSelection()
        {
            foreach (var navigatable in _navigatables)
            {
                navigatable.SetSelected(false, SelectionOrigin.Programmatically);
            }
        }

        public void ConfirmSelection()
        {
            if (SelectedBehaviour != null)
            {
                SelectedBehaviour.Confirm();
            }
        }

        public void PushNavGroupToStack()
        {
            if (_canBeCurrent && CurrentNavigationGroup != this)
            {
                _navGroupsStack.Add(this);
            }
        }

        public void SelectLastNavGroup()
        {
            if (CurrentNavigationGroup != this)
            {
                return;
            }

            ClearSelection();
            _navGroupsStack.Remove(this);
        }
    }
}
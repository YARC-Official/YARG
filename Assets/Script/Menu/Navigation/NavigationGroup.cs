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
                CurrentNavigationGroup = this;
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

        public int CurrentSelectedIndex()
        {
            for (int i = 0; i < _navigatables.Count; i++)
            {
                if (_navigatables[i].Selected)
                {
                    return i;
                }
            }

            return -1;
        }

        public void SelectNext(NavigationContext context)
        {
            int selected = CurrentSelectedIndex();
            if (selected == -1) return;

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
            int selected = CurrentSelectedIndex();
            if (selected == -1) return;

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
            var nav = _navigatables[CurrentSelectedIndex()];
            if (nav is INavigationConfirmable confirmable)
            {
                confirmable.Confirm();
            }
        }
    }
}
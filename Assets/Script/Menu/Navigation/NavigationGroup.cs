using System.Collections.Generic;
using UnityEngine;

namespace YARG.Menu.Navigation
{
    public sealed class NavigationGroup : MonoBehaviour
    {
        private readonly List<NavigatableBehaviour> _navigatables = new();

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
    }
}
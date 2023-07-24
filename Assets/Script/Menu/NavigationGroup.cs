using System.Collections.Generic;
using UnityEngine;

namespace YARG.Menu
{
    public class NavigationGroup : MonoBehaviour
    {
        private List<NavigatableBehaviour> _navigatables = new();

        public void AddNavigatable(NavigatableBehaviour n)
        {
            _navigatables.Add(n);
            n.NavigationGroup = this;
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
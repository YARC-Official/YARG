using UnityEngine;
using YARG.Helpers.Extensions;

namespace YARG.Menu.Dialogs
{
    public class ListDialog : Dialog
    {
        [Space]
        [SerializeField]
        private Transform _listContainer;

        public T AddListEntry<T>(T prefab)
            where T : Object
        {
            return Instantiate(prefab, _listContainer.transform);
        }

        public void ClearList()
        {
            _listContainer.DestroyChildren();
        }
    }
}

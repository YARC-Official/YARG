using UnityEngine;
using UnityEngine.Events;
using YARG.Helpers.Extensions;

namespace YARG.Menu.Dialogs
{
    public class ListDialog : Dialog
    {
        [Space]
        [SerializeField]
        private Transform _listContainer;
        [SerializeField]
        private ColoredButton _listButtonPrefab;

        public ColoredButton AddListButton(string text, UnityAction handler)
        {
            var button = AddListEntry(_listButtonPrefab);

            button.Text.text = text;
            button.OnClick.AddListener(handler);

            return button;
        }

        public T AddListEntry<T>(T prefab)
            where T : Object
        {
            return Instantiate(prefab, _listContainer.transform);
        }

        public void ClearList()
        {
            _listContainer.DestroyChildren();
        }

        public override void ClearDialog()
        {
            base.ClearDialog();

            ClearList();
        }
    }
}

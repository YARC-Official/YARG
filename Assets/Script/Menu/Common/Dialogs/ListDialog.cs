using UnityEngine;
using UnityEngine.Events;
using YARG.Helpers.Extensions;
using YARG.Menu.Persistent;

namespace YARG.Menu.Dialogs
{
    public class ListDialog : Dialog
    {
        [Space]
        [SerializeField]
        private Transform _listContainer;
        [SerializeField]
        private ColoredButton _listButtonPrefab;

        public ColoredButton AddListButton(string text, UnityAction handler = null, bool closeOnClick = true)
        {
            var button = AddListEntry(_listButtonPrefab);

            button.Text.text = text;
            if (closeOnClick)
            {
                if (handler != null)
                {
                    button.OnClick.AddListener(() =>
                    {
                        handler();
                        DialogManager.Instance.ClearDialog();
                    });
                }
                else
                {
                    button.OnClick.AddListener(() => DialogManager.Instance.ClearDialog());
                }
            }
            else if (handler != null)
            {
                button.OnClick.AddListener(handler);
            }

            return button;
        }

        public T AddListEntry<T>(T prefab)
            where T : Object
        {
            return Instantiate(prefab, _listContainer);
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

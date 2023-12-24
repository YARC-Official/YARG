using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Helpers.Extensions;

namespace YARG.Menu
{
    public class DropdownDrawer : MonoBehaviour
    {
        [SerializeField]
        private GameObject _foldout;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _text;
        [SerializeField]
        private GameObject _arrow;

        public string Text
        {
            get => _text.text;
            set => _text.text = value;
        }

        private bool _drawerOpened;
        public bool DrawerOpened
        {
            get => _drawerOpened;
            set
            {
                SetDrawerWithoutRebuild(value);
                RebuildLayout();
            }
        }

        private void Awake()
        {
            DrawerOpened = false;
        }

        public T AddNew<T>(T prefab)
            where T : Object
        {
            var instance = Instantiate(prefab, _foldout.transform);
            RebuildLayout();
            return instance;
        }

        public T AddNewWithoutRebuild<T>(T prefab)
            where T : Object
        {
            var instance = Instantiate(prefab, _foldout.transform);
            RebuildLayout();
            return instance;
        }

        public void ClearDrawer()
        {
            _foldout.transform.DestroyChildren();
        }

        public void SetDrawerWithoutRebuild(bool open)
        {
            // Set the state of the drawer
            _drawerOpened = open;
            _foldout.SetActive(open);

            // Flip the arrow graphic
            float arrowScale = open ? -1f : 1f;
            _arrow.transform.localScale = _arrow.transform.localScale.WithY(arrowScale);

        }

        public void ToggleDrawer() => DrawerOpened = !DrawerOpened;

        public void ToggleDrawerWithoutRebuild() => SetDrawerWithoutRebuild(!DrawerOpened);

        public void RebuildLayout()
        {
            if (transform is RectTransform rect)
            {
                rect.ForceUpdateRectTransforms();
                LayoutRebuilder.MarkLayoutForRebuild(rect);
            }
        }
    }
}
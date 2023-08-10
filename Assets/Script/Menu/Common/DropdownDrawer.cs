using TMPro;
using UnityEngine;

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
                // Set the state of the drawer
                _drawerOpened = value;
                _foldout.SetActive(value);

                // Flip the arrow graphic
                float arrowScale = value ? -1f : 1f;
                _arrow.transform.localScale = _arrow.transform.localScale.WithY(arrowScale);
            }
        }

        private void Awake()
        {
            DrawerOpened = false;
        }

        public T AddNewPrefabInstance<T>(T prefab)
            where T : Object
        {
            return Instantiate(prefab, _foldout.transform);
        }

        public void ToggleDrawer() => DrawerOpened = !DrawerOpened;
    }
}
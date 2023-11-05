using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace YARG.Menu.ListMenu
{
    public abstract class ListMenu<TViewType, TViewObject> : MonoBehaviour
        where TViewType : BaseViewType
        where TViewObject : ViewObject<TViewType>
    {
        private const float SCROLL_TIME = 1f / 60f;

        protected abstract int ExtraListViewPadding { get; }

        [SerializeField]
        private TViewObject _viewObjectPrefab;
        [SerializeField]
        private Scrollbar _scrollbar;

        private List<TViewType> _viewList;
        private List<TViewObject> _viewObjects;

        private int _selectedIndex;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                // Properly wrap the value. If the value is less than zero,
                // wrap the to the end. If more than the end, wrap to zero.
                _selectedIndex = value < 0 ? _viewList.Count - 1 : value % _viewList.Count;

                OnSelectedIndexChanged();
            }
        }

        protected virtual bool CanScroll => true;
        private float _scrollTimer;

        protected virtual void OnSelectedIndexChanged()
        {
            UpdateScrollbar();
            UpdateViewsObjects();
        }

        public void OnScrollBarChange()
        {
            SelectedIndex = Mathf.FloorToInt(_scrollbar.value * (_viewList.Count - 1));
        }

        private void UpdateScrollbar()
        {
            _scrollbar.SetValueWithoutNotify((float) SelectedIndex / _viewList.Count);
        }

        private void UpdateViewsObjects()
        {
            for (int i = 0; i < _viewObjects.Count; i++)
            {
                // Hide if it's not in range
                int relativeIndex = i - ExtraListViewPadding;
                int realIndex = SelectedIndex + relativeIndex;
                if (realIndex < 0 || realIndex >= _viewList.Count)
                {
                    _viewObjects[i].Hide();
                    continue;
                }

                // Otherwise, show
                _viewObjects[i].Show(relativeIndex == 0, _viewList[realIndex]);
            }
        }

        protected virtual void Update()
        {
            UpdateScroll();
        }

        private void UpdateScroll()
        {
            if (!CanScroll) return;

            if (_scrollTimer > 0f)
            {
                _scrollTimer -= Time.deltaTime;
                return;
            }

            var delta = Mouse.current.scroll.ReadValue().y * Time.deltaTime;

            if (delta > 0f)
            {
                SelectedIndex--;
                _scrollTimer = SCROLL_TIME;
                return;
            }

            if (delta < 0f)
            {
                SelectedIndex++;
                _scrollTimer = SCROLL_TIME;
            }
        }
    }
}
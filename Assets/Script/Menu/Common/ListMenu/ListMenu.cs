using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Settings;

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

        [Space]
        [SerializeField]
        private Transform _viewObjectParent;
        [SerializeField]
        private Scrollbar _scrollbar;
        [SerializeField]
        private ViewAligner _viewAligner;

        private List<TViewType> _viewList;
        private readonly List<TViewObject> _viewObjects = new();

        private bool _allowWrapAround;

        public IReadOnlyList<TViewType> ViewList => _viewList;

        private int _selectedIndex;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_viewList.Count == 0)
                {
                    _selectedIndex = 0;
                }
                else if (_allowWrapAround)
                {
                    // Wrap to bottom/top of list when moving past the start/end range
                    if (value > _viewList.Count - 1)
                    {
                        _selectedIndex = 0;
                    }
                    else if (value < 0)
                    {
                        _selectedIndex = _viewList.Count - 1;
                    }
                    else
                    {
                        _selectedIndex = value;
                    }
                }
                else
                {
                    // Do not allow selection to move past the start or end range
                    _selectedIndex = Mathf.Clamp(value, 0, _viewList.Count - 1);
                }

                OnSelectedIndexChanged();
            }
        }

        public TViewType CurrentSelection => _viewList?.Count == 0 ? null : _viewList?[_selectedIndex];

        protected virtual bool CanScroll => true;
        private float _scrollTimer;

        protected virtual void Awake()
        {
            // Create all of the replay views
            for (int i = 0; i < ExtraListViewPadding * 2 + 1; i++)
            {
                var gameObject = Instantiate(_viewObjectPrefab, _viewObjectParent);

                // Add
                var view = gameObject.GetComponent<TViewObject>();
                _viewObjects.Add(view);

                // If the middle one...
                if (i == ExtraListViewPadding && _viewAligner != null)
                {
                    // Provide it to the view aligner
                    _viewAligner.SelectedView = gameObject.GetComponent<RectTransform>();
                }
            }

            RequestViewListUpdate();
        }

        protected virtual void OnSelectedIndexChanged()
        {
            UpdateScrollbar();
            RefreshViewsObjects();

            if (_viewAligner != null)
            {
                // Make sure to update the canvases since we *just* changed the view objects
                Canvas.ForceUpdateCanvases();
                _viewAligner.RequestAlignView();
            }
        }

        /// <summary>
        /// Sets the <see cref="SelectedIndex"/> to the first match (via the <paramref name="predicate"/>).
        /// If the <paramref name="searchStartIndex"/> is specified, it will offset the select index by that amount.
        /// If nothing is found, the index remains unchanged.
        /// </summary>
        /// <returns>
        /// Whether or not the index was set.
        /// </returns>
        protected bool SetIndexTo(Predicate<TViewType> predicate, int searchStartIndex = 0)
        {
            for (int i = searchStartIndex; i < _viewList.Count; i++)
            {
                if (predicate(_viewList[i]))
                {
                    SelectedIndex = i;
                    return true;
                }
            }

            return false;
        }

        public void OnScrollBarChange()
        {
            SelectedIndex = Mathf.FloorToInt(_scrollbar.value * (_viewList.Count - 1));
        }

        public void SetWrapAroundState(bool newState)
        {
            if (SettingsManager.Settings.WrapAroundNavigation.Value)
            {
                _allowWrapAround = newState;
            }
            else if (_allowWrapAround)
            {
                _allowWrapAround = false;
            }
        }

        private void UpdateScrollbar()
        {
            _scrollbar.SetValueWithoutNotify((float) SelectedIndex / _viewList.Count);
        }

        protected void RequestViewListUpdate()
        {
            _viewList = CreateViewList();
            RefreshViewsObjects();
        }

        protected abstract List<TViewType> CreateViewList();

        public void RefreshViewsObjects()
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
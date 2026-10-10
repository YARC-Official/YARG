using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YARG.Menu.Navigation;

namespace YARG.Menu.Settings
{
    [RequireComponent(typeof(NavigationGroup))]
    public sealed class CategorySidebar : MonoBehaviour, IPointerDownHandler
    {
        public const float EXPANDED_WIDTH = 240f;
        public const float COLLAPSED_WIDTH = 64f;
        private const float ANIM_DURATION = 0.15f;
        private const float FADE_DURATION = 0.12f;

        [Serializable]
        public struct CategoryInfo
        {
            public string Id;
            public string DisplayName;
            public Sprite Icon;
        }

        [SerializeField]
        private LayoutElement _layoutElement;

        [SerializeField]
        private Transform _container;

        [SerializeField]
        private CategoryItemView _itemPrefab;

        private NavigationGroup _navigationGroup;
        private Tween _widthTween;
        private readonly List<CategoryItemView> _views = new();

        public bool IsCollapsed { get; private set; }
        public string SelectedCategoryId { get; private set; }
        public NavigationGroup NavigationGroup
        {
            get
            {
                EnsureInitialized();
                return _navigationGroup;
            }
        }

        public event Action<string> CategoryChanged;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (_navigationGroup != null)
            {
                return;
            }

            _navigationGroup = GetComponent<NavigationGroup>();
            _navigationGroup.SelectionChanged += OnSelectionChanged;
        }

        public void SetCategories(IReadOnlyList<CategoryInfo> categories)
        {
            EnsureInitialized();
            _views.Clear();
            _navigationGroup.ClearNavigatables();

            for (var i = 0; i < categories.Count; i++)
            {
                var info = categories[i];
                var view = Instantiate(_itemPrefab, _container);
                _navigationGroup.AddNavigatable(view);
                SettingsItemVisual.Attach(view);
                view.Initialize(info.Id, info.DisplayName, info.Icon);
                view.SetLabelAlpha(IsCollapsed ? 0f : 1f, 0f);
                _views.Add(view);
            }
        }

        public void SelectCategory(string categoryId)
        {
            for (var i = 0; i < _views.Count; i++)
            {
                if (_views[i].CategoryId == categoryId)
                {
                    SelectedCategoryId = categoryId;
                    _navigationGroup.PushNavGroupToStack();
                    if (_navigationGroup.SelectedIndex != i)
                    {
                        _navigationGroup.SelectAt(i, SelectionOrigin.Navigation);
                    }
                    else
                    {
                        _views[i].SetSelected(true, SelectionOrigin.Navigation);
                    }

                    UpdateMarkers(categoryId);
                    return;
                }
            }
        }

        public void SetCollapsed(bool collapsed, bool animate = true)
        {
            if (IsCollapsed == collapsed && Mathf.Approximately(_layoutElement.preferredWidth, collapsed ? COLLAPSED_WIDTH : EXPANDED_WIDTH))
            {
                return;
            }

            IsCollapsed = collapsed;
            var targetWidth = collapsed ? COLLAPSED_WIDTH : EXPANDED_WIDTH;
            var targetAlpha = collapsed ? 0f : 1f;

            _widthTween?.Kill();

            if (!animate)
            {
                _layoutElement.preferredWidth = targetWidth;
                for (var i = 0; i < _views.Count; i++)
                {
                    _views[i].SetLabelAlpha(targetAlpha, 0f);
                }
                return;
            }

            _widthTween = DOTween.To(() => _layoutElement.preferredWidth, x => _layoutElement.preferredWidth = x, targetWidth, ANIM_DURATION)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(gameObject);

            for (var i = 0; i < _views.Count; i++)
            {
                _views[i].SetLabelAlpha(targetAlpha, FADE_DURATION);
            }
        }

        private void OnSelectionChanged(NavigatableBehaviour selected, SelectionOrigin origin)
        {
            if (selected is not CategoryItemView view)
            {
                return;
            }

            SelectedCategoryId = view.CategoryId;
            UpdateMarkers(SelectedCategoryId);
            CategoryChanged?.Invoke(SelectedCategoryId);
        }

        private void UpdateMarkers(string currentId)
        {
            for (var i = 0; i < _views.Count; i++)
            {
                _views[i].GetComponent<SettingsItemVisual>().ShowCurrent(_views[i].CategoryId == currentId);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            SettingsMenu.Instance.FocusCategories();
        }
    }
}

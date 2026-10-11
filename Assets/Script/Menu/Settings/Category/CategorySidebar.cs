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

        public readonly struct CategoryInfo
        {
            public CategoryInfo(string id, string displayName, Sprite icon)
            {
                Id = id;
                DisplayName = displayName;
                Icon = icon;
            }

            public readonly string Id;
            public readonly string DisplayName;
            public readonly Sprite Icon;
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
                if (_navigationGroup == null)
                {
                    _navigationGroup = GetComponent<NavigationGroup>();
                    _navigationGroup.SelectionChanged += OnSelectionChanged;
                }
                return _navigationGroup;
            }
        }

        public event Action<string> CategoryChanged;

        private void Awake()
        {
            _ = NavigationGroup;
        }

        private void OnDestroy()
        {
            if (_navigationGroup != null)
            {
                _navigationGroup.SelectionChanged -= OnSelectionChanged;
            }
        }

        public void SetCategories(IReadOnlyList<CategoryInfo> categories)
        {
            _views.Clear();
            NavigationGroup.ClearNavigatables();

            foreach (var info in categories)
            {
                var view = Instantiate(_itemPrefab, _container);
                NavigationGroup.AddNavigatable(view);
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
                    NavigationGroup.PushNavGroupToStack();
                    if (NavigationGroup.SelectedIndex != i)
                    {
                        NavigationGroup.SelectAt(i, SelectionOrigin.Navigation);
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
            var targetWidth = collapsed ? COLLAPSED_WIDTH : EXPANDED_WIDTH;
            if (IsCollapsed == collapsed && Mathf.Approximately(_layoutElement.preferredWidth, targetWidth))
            {
                return;
            }

            IsCollapsed = collapsed;
            var targetAlpha = collapsed ? 0f : 1f;

            _widthTween?.Kill();

            if (!animate)
            {
                _layoutElement.preferredWidth = targetWidth;
                foreach (var view in _views)
                {
                    view.SetLabelAlpha(targetAlpha, 0f);
                }
                return;
            }

            _widthTween = DOTween.To(() => _layoutElement.preferredWidth, x => _layoutElement.preferredWidth = x, targetWidth, ANIM_DURATION)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(gameObject);

            foreach (var view in _views)
            {
                view.SetLabelAlpha(targetAlpha, FADE_DURATION);
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
            foreach (var view in _views)
            {
                view.SetCurrent(view.CategoryId == currentId);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            SettingsMenu.Instance.FocusCategories();
        }
    }
}

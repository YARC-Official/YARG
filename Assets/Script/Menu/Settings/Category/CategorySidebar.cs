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
            public string Id { get; }
            public string DisplayName { get; }
            public Sprite Icon { get; }

            public CategoryInfo(string id, string displayName, Sprite icon)
            {
                Id = id;
                DisplayName = displayName;
                Icon = icon;
            }
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
        public NavigationGroup NavigationGroup =>
            _navigationGroup != null ? _navigationGroup : _navigationGroup = GetComponent<NavigationGroup>();

        public event Action<string> CategoryChanged;
        public event Action<string> CategoryClicked;
        public event Action<string> CategoryConfirmed;
        public event Action SidebarClicked;

        private void OnEnable()
        {
            NavigationGroup.SelectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            NavigationGroup.SelectionChanged -= OnSelectionChanged;
        }

        public void SetCategories(IReadOnlyList<CategoryInfo> categories)
        {
            _views.Clear();
            NavigationGroup.ClearNavigatables();

            var labelAlpha = IsCollapsed ? 0f : 1f;
            foreach (var info in categories)
            {
                var view = Instantiate(_itemPrefab, _container);
                NavigationGroup.AddNavigatable(view);
                view.Initialize(info.Id, info.DisplayName, info.Icon,
                    onClicked: id => CategoryClicked?.Invoke(id),
                    onConfirmed: id => CategoryConfirmed?.Invoke(id));
                view.SetLabelAlpha(alpha: labelAlpha, duration: 0f);
                _views.Add(view);
            }
        }

        public void SelectCategory(string categoryId)
        {
            for (var i = 0; i < _views.Count; i++)
            {
                var view = _views[i];
                if (view.CategoryId != categoryId)
                {
                    continue;
                }

                SelectedCategoryId = categoryId;
                NavigationGroup.PushNavGroupToStack();
                if (NavigationGroup.SelectedIndex != i)
                {
                    NavigationGroup.SelectAt(i, SelectionOrigin.Navigation);
                }
                else
                {
                    view.SetSelected(selected: true, SelectionOrigin.Navigation);
                }

                UpdateMarkers(categoryId);
                return;
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
            _widthTween?.Kill();

            if (!animate)
            {
                _layoutElement.preferredWidth = targetWidth;
            }
            else
            {
                _widthTween = DOTween.To(() => _layoutElement.preferredWidth, x => _layoutElement.preferredWidth = x, targetWidth, ANIM_DURATION)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(isIndependentUpdate: true)
                    .SetLink(gameObject);
            }

            var targetAlpha = collapsed ? 0f : 1f;
            var duration = animate ? FADE_DURATION : 0f;
            foreach (var view in _views)
            {
                view.SetLabelAlpha(alpha: targetAlpha, duration: duration);
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

        public void OnPointerDown(PointerEventData eventData) => SidebarClicked?.Invoke();
    }
}

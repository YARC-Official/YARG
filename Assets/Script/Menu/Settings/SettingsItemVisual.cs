using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YARG.Menu.Navigation;
using YARG.Menu.Settings.AllSettings;
using YARG.Menu.Settings.Visuals;

namespace YARG.Menu.Settings
{
    public sealed class SettingsItemVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        private static readonly Color CURRENT_COLOR = new(0.035f, 0.3f, 0.42f, 0.9f);
        private static readonly Color HOVER_COLOR = new(0.28f, 0.55f, 0.65f, 0.15f);

        private NavigatableBehaviour _navigatable;
        [SerializeField]
        private Image _background;
        [SerializeField]
        private Image _marker;
        [SerializeField]
        private RectTransform _focus;
        [SerializeField]
        private GameObject _focusFill;
        [SerializeField]
        private Image[] _focusEdges = Array.Empty<Image>();
        [SerializeField]
        private Transform _controls;
        private bool _hovered;
        private bool _current;

        private bool IsSidebar => _navigatable is CategoryItemView or SettingsSectionView;
        private bool IsCurrent => (IsSidebar ? _current : _navigatable.Selected) &&
            SettingsMenu.Instance.IsNavigationLevelActive(_navigatable.NavigationGroup);

        public bool HasFocus => _navigatable.Selected &&
            NavigationGroup.CurrentNavigationGroup == _navigatable.NavigationGroup;

        private void Awake()
        {
            _navigatable = GetComponent<NavigatableBehaviour>();
            if (_navigatable != null)
            {
                _navigatable.SelectOnHover = false;
                if (_navigatable is RuntimeNavigatable runtime)
                {
                    runtime.SelectionVisual?.Invoke(false);
                    runtime.SelectionVisual = _ => { };
                }
            }

            if (TryGetComponent<BaseSettingVisual>(out _))
            {
                foreach (var control in GetComponentsInChildren<Selectable>(true))
                {
                    if (!control.TryGetComponent<SettingsControlFocus>(out var focus))
                    {
                        focus = control.gameObject.AddComponent<SettingsControlFocus>();
                    }
                    focus.Item = this;
                }
            }
        }

        private void OnEnable()
        {
            if (_navigatable != null)
            {
                _navigatable.SelectionStateChanged += OnSelectionChanged;
                if (_navigatable is BaseSettingNavigatable setting)
                {
                    setting.EditingChanged += SetEditing;
                }
            }
            NavigationGroup.CurrentChanged += Refresh;
        }

        private void Start()
        {
            if (_navigatable != null)
            {
                OnSelectionChanged(navigatable: _navigatable, selected: _navigatable.Selected,
                    origin: SelectionOrigin.Programmatically);
            }
            else
            {
                Refresh();
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public void ShowCurrent(bool current)
        {
            _current = current;
            Refresh();
        }

        public void SetEditing(bool editing)
        {
            var target = editing ? _controls : transform;
            if (editing)
            {
                var controls = target.GetComponentsInChildren<Selectable>();
                if (controls.Length == 1)
                {
                    target = controls[0].transform;
                }
            }
            SetFocusTarget(target, editing);
            Refresh();
        }

        public void FocusControl(Transform control)
        {
            SettingsMenu.Instance.OnSettingClicked(_navigatable);
            var editing = control.GetComponent<Selectable>() is not Toggle;
            SetFocusTarget(editing ? control : transform, editing);
            Refresh();
        }

        private void SetFocusTarget(Transform target, bool editing)
        {
            if (_focus == null)
            {
                return;
            }

            _focus.SetParent(target, worldPositionStays: false);
            Stretch(_focus);
            if (editing)
            {
                _focus.SetAsLastSibling();
            }
            else
            {
                _focus.SetSiblingIndex(1);
            }
            foreach (var edge in _focusEdges)
            {
                edge.gameObject.SetActive(editing);
            }
            _focusFill?.SetActive(!editing);
        }

        public void ReleaseControl()
        {
            SetEditing(_navigatable is BaseSettingNavigatable { IsFocused: true });
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Refresh();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsSidebar && _navigatable is not SettingSearchResult)
            {
                SettingsMenu.Instance.OnSettingClicked(_navigatable);
            }
        }

        private void OnSelectionChanged(NavigatableBehaviour navigatable, bool selected, SelectionOrigin origin)
        {
            Refresh();
        }

        private void Refresh()
        {
            var current = IsCurrent;
            if (_focus != null)
            {
                _focus.gameObject.SetActive(HasFocus);
            }

            if (_navigatable is CategoryItemView category)
            {
                category.ShowActive(current);
            }
            else if (_navigatable is SettingsSectionView section)
            {
                section.ShowActive(current);
            }
            else if (_marker != null)
            {
                _marker.gameObject.SetActive(current);
            }

            var background = current ? CURRENT_COLOR : Color.clear;
            if (_background != null)
            {
                _background.color = _hovered && !current ? HOVER_COLOR : background;
            }
        }

        private void OnDisable()
        {
            if (_navigatable != null)
            {
                _navigatable.SelectionStateChanged -= OnSelectionChanged;
                if (_navigatable is BaseSettingNavigatable setting)
                {
                    setting.EditingChanged -= SetEditing;
                }
            }
            NavigationGroup.CurrentChanged -= Refresh;
            _hovered = false;
            if (_background != null)
            {
                _background.color = IsCurrent ? CURRENT_COLOR : Color.clear;
            }
        }
    }
}

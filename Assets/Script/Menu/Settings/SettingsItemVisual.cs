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
        private static readonly Color CYAN = new(0f, 0.8f, 1f);
        private static readonly Color CURRENT_COLOR = new(0.035f, 0.3f, 0.42f, 0.9f);
        private static readonly Color HOVER_COLOR = new(0.28f, 0.55f, 0.65f, 0.15f);
        private static readonly Color FOCUS_COLOR = new(0.06f, 0.55f, 0.75f, 0.2f);

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
        private Image[] _hiddenImages = Array.Empty<Image>();
        [SerializeField]
        private Image[] _evenBackgrounds = Array.Empty<Image>();
        [SerializeField]
        private Image[] _dividers = Array.Empty<Image>();
        [SerializeField]
        private Transform _controls;
        private bool _hovered;
        private bool _current;

        private bool IsSidebar => _navigatable is CategoryItemView or SettingsSectionView;
        private bool IsCurrent => (IsSidebar ? _current : _navigatable.Selected) &&
            SettingsMenu.Instance.IsNavigationLevelActive(_navigatable.NavigationGroup);

        public bool HasFocus => _navigatable.Selected &&
            NavigationGroup.CurrentNavigationGroup == _navigatable.NavigationGroup;

        public static void Attach(NavigatableBehaviour navigatable)
        {
            if (!navigatable.TryGetComponent<SettingsItemVisual>(out var visual))
            {
                visual = navigatable.gameObject.AddComponent<SettingsItemVisual>();
                visual._controls = navigatable.transform;
                visual.CreateGraphics();
            }
            visual.Initialize(navigatable);
            visual.enabled = true;
        }

        private void Initialize(NavigatableBehaviour navigatable)
        {
            _navigatable = navigatable;
            navigatable.SelectOnHover = false;

            if (navigatable is RuntimeNavigatable runtime)
            {
                runtime.SelectionVisual?.Invoke(false);
                runtime.SelectionVisual = _ => { };
            }
            else
            {
                foreach (var image in navigatable.SelectedVisual.GetComponentsInChildren<Image>(true))
                {
                    image.color = Color.clear;
                    image.raycastTarget = false;
                }
            }

            foreach (var image in _hiddenImages)
            {
                image.color = Color.clear;
                image.raycastTarget = false;
            }
            foreach (var image in _evenBackgrounds)
            {
                image.color = new Color(0.04f, 0.11f, 0.16f, 0.2f);
                image.raycastTarget = false;
            }
            foreach (var image in _dividers)
            {
                image.color = new Color(0.15f, 0.28f, 0.35f, 0.15f);
                image.raycastTarget = false;
            }
            _background.gameObject.SetActive(true);
            if (navigatable.TryGetComponent<BaseSettingVisual>(out _))
            {
                foreach (var control in GetComponentsInChildren<Selectable>())
                {
                    control.gameObject.AddComponent<SettingsControlFocus>().Item = this;
                }
            }

            Refresh();
        }

        private void CreateGraphics()
        {
            _background = CreateImage(name: "Hover and Current", parent: transform, color: Color.clear);
            _background.raycastTarget = true;
            _background.transform.SetAsFirstSibling();
            _background.gameObject.SetActive(false);

            _marker = CreateImage(name: "Current Setting", parent: transform, color: CYAN);
            var marker = _marker.rectTransform;
            marker.anchorMin = Vector2.zero;
            marker.anchorMax = new Vector2(0f, 1f);
            marker.offsetMin = Vector2.zero;
            marker.offsetMax = new Vector2(4f, 0f);
            _marker.gameObject.SetActive(false);

            var focus = new GameObject("Focus Highlight",
                typeof(RectTransform), typeof(LayoutElement));
            _focus = focus.GetComponent<RectTransform>();
            _focus.SetParent(transform, worldPositionStays: false);
            Stretch(_focus);
            focus.GetComponent<LayoutElement>().ignoreLayout = true;
            _focusEdges = new[]
            {
                AddEdge(name: "Top", anchorMin: new Vector2(0f, 1f), anchorMax: Vector2.one,
                    offsetMin: new Vector2(0f, -2f), offsetMax: Vector2.zero),
                AddEdge(name: "Bottom", anchorMin: Vector2.zero, anchorMax: new Vector2(1f, 0f),
                    offsetMin: Vector2.zero, offsetMax: new Vector2(0f, 2f)),
                AddEdge(name: "Left", anchorMin: Vector2.zero, anchorMax: new Vector2(0f, 1f),
                    offsetMin: Vector2.zero, offsetMax: new Vector2(2f, 0f)),
                AddEdge(name: "Right", anchorMin: new Vector2(1f, 0f), anchorMax: Vector2.one,
                    offsetMin: new Vector2(-2f, 0f), offsetMax: Vector2.zero)
            };
            foreach (var edge in _focusEdges)
            {
                edge.gameObject.SetActive(false);
            }
            _focusFill = CreateImage(name: "Focus Fill", parent: _focus, color: FOCUS_COLOR).gameObject;
            _focus.SetSiblingIndex(1);
            focus.SetActive(false);
        }

        private void Awake()
        {
            _navigatable = GetComponent<NavigatableBehaviour>();
        }

        private void OnEnable()
        {
            _navigatable.SelectionStateChanged += OnSelectionChanged;
            if (_navigatable is BaseSettingNavigatable setting)
            {
                setting.EditingChanged += SetEditing;
            }
            NavigationGroup.CurrentChanged += Refresh;
        }

        private void Start()
        {
            OnSelectionChanged(navigatable: _navigatable, selected: _navigatable.Selected,
                origin: SelectionOrigin.Programmatically);
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, worldPositionStays: false);
            Stretch(rect);
            child.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = child.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private Image AddEdge(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var image = CreateImage(name: name, parent: _focus, color: CYAN);
            var edge = image.rectTransform;
            edge.anchorMin = anchorMin;
            edge.anchorMax = anchorMax;
            edge.offsetMin = offsetMin;
            edge.offsetMax = offsetMax;
            return image;
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
            _focusFill.SetActive(!editing);
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
            if (selected && navigatable.TryGetComponent<BaseSettingVisual>(out var setting))
            {
                setting.SettingLabel.color = Color.white;
            }
            Refresh();
        }

        private void Refresh()
        {
            var current = IsCurrent;
            _focus.gameObject.SetActive(HasFocus);
            if (_navigatable is CategoryItemView category)
            {
                category.ShowActive(current);
            }
            else if (_navigatable is SettingsSectionView section)
            {
                section.ShowActive(current);
            }
            else
            {
                _marker.gameObject.SetActive(current);
            }
            var background = current ? CURRENT_COLOR : Color.clear;
            _background.color = _hovered && !current ? HOVER_COLOR : background;
        }

        private void OnDisable()
        {
            _navigatable.SelectionStateChanged -= OnSelectionChanged;
            if (_navigatable is BaseSettingNavigatable setting)
            {
                setting.EditingChanged -= SetEditing;
            }
            NavigationGroup.CurrentChanged -= Refresh;
            _hovered = false;
            _background.color = IsCurrent ? CURRENT_COLOR : Color.clear;
        }
    }
}

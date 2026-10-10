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
        private Image _background;
        private Image _marker;
        private RectTransform _focus;
        private bool _hovered;
        private bool _current;

        private bool IsSidebar => _navigatable is CategoryItemView or SettingsSectionView;
        private bool IsCurrent => (IsSidebar ? _current : _navigatable.Selected) &&
            SettingsMenu.Instance.IsNavigationLevelActive(_navigatable.NavigationGroup);

        public bool HasFocus => _navigatable.Selected &&
            NavigationGroup.CurrentNavigationGroup == _navigatable.NavigationGroup;

        public static void Attach(NavigatableBehaviour navigatable)
        {
            var visual = navigatable.gameObject.AddComponent<SettingsItemVisual>();
            visual.Initialize(navigatable);
        }

        private void Initialize(NavigatableBehaviour navigatable)
        {
            _navigatable = navigatable;
            navigatable.SelectOnHover = false;

            if (navigatable is RuntimeNavigatable runtime)
            {
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

            foreach (var image in GetComponentsInChildren<Image>(true))
            {
                if (image.name == "SelectedOutline" || image.name == "DropdownFocusFill" ||
                    image.name == "Active Background")
                {
                    image.color = Color.clear;
                    image.raycastTarget = false;
                }
                else if (image.name == "Even Background")
                {
                    image.color = new Color(0.04f, 0.11f, 0.16f, 0.2f);
                    image.raycastTarget = false;
                }
                else if (image.name == "Divider" || image.name == "Divder")
                {
                    image.color = new Color(0.15f, 0.28f, 0.35f, 0.15f);
                    image.raycastTarget = false;
                }
                else if (image.name == "ActiveMarker" || image.name == "Current Section")
                {
                    _marker = image;
                }
            }

            _background = CreateImage(name: "Hover and Current", parent: transform, color: Color.clear);
            _background.raycastTarget = true;
            _background.transform.SetAsFirstSibling();

            if (!IsSidebar)
            {
                _marker = CreateImage(name: "Current Setting", parent: transform, color: CYAN);
            }
            _marker.color = CYAN;
            var marker = _marker.rectTransform;
            marker.anchorMin = Vector2.zero;
            marker.anchorMax = new Vector2(0f, 1f);
            marker.offsetMin = Vector2.zero;
            marker.offsetMax = new Vector2(4f, 0f);

            var focus = new GameObject(IsSidebar ? "Focus Outline" : "Focus Highlight",
                typeof(RectTransform), typeof(LayoutElement));
            _focus = focus.GetComponent<RectTransform>();
            _focus.SetParent(transform, worldPositionStays: false);
            Stretch(_focus);
            focus.GetComponent<LayoutElement>().ignoreLayout = true;

            AddEdge("Top", anchorMin: new Vector2(0f, 1f), anchorMax: Vector2.one,
                offsetMin: new Vector2(0f, -2f), offsetMax: Vector2.zero);
            AddEdge("Bottom", anchorMin: Vector2.zero, anchorMax: new Vector2(1f, 0f),
                offsetMin: Vector2.zero, offsetMax: new Vector2(0f, 2f));
            AddEdge("Left", anchorMin: Vector2.zero, anchorMax: new Vector2(0f, 1f),
                offsetMin: Vector2.zero, offsetMax: new Vector2(2f, 0f));
            AddEdge("Right", anchorMin: new Vector2(1f, 0f), anchorMax: Vector2.one,
                offsetMin: new Vector2(-2f, 0f), offsetMax: Vector2.zero);

            if (!IsSidebar)
            {
                foreach (Transform edge in _focus)
                {
                    edge.gameObject.SetActive(false);
                }
                CreateImage(name: "Focus Fill", parent: _focus, color: FOCUS_COLOR);
                _focus.SetSiblingIndex(1);
            }

            if (navigatable.TryGetComponent<BaseSettingVisual>(out _))
            {
                foreach (var control in GetComponentsInChildren<Selectable>())
                {
                    control.gameObject.AddComponent<SettingsControlFocus>().Item = this;
                }
            }

            Refresh();
        }

        private void Start()
        {
            _navigatable.SelectionStateChanged += OnSelectionChanged;
            NavigationGroup.CurrentChanged += Refresh;
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

        private void AddEdge(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var edge = CreateImage(name: name, parent: _focus, color: CYAN).rectTransform;
            edge.anchorMin = anchorMin;
            edge.anchorMax = anchorMax;
            edge.offsetMin = offsetMin;
            edge.offsetMax = offsetMax;
        }

        public void ShowCurrent(bool current)
        {
            _current = current;
            Refresh();
        }

        public void SetEditing(bool editing)
        {
            var target = editing ? transform.Find("Container") : transform;
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
            foreach (Transform edge in _focus)
            {
                edge.gameObject.SetActive(editing);
            }
            _focus.Find("Focus Fill").gameObject.SetActive(!editing);
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
            _hovered = false;
            _background.color = IsCurrent ? CURRENT_COLOR : Color.clear;
        }

        private void OnDestroy()
        {
            _navigatable.SelectionStateChanged -= OnSelectionChanged;
            NavigationGroup.CurrentChanged -= Refresh;
        }
    }
}

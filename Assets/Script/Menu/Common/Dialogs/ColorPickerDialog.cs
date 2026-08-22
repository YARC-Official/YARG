using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;

namespace YARG.Menu.Dialogs
{
    public class ColorPickerDialog : Dialog
    {
        public enum SliderType
        {
            H = 0, S = 1, V = 2,
            R = 3, G = 4, B = 5
        }

        [Serializable]
        public struct TypeSliderPair
        {
            public SliderType Type;
            public ValueSlider Slider;
        }

        // Seconds between value steps while a direction is held during a
        // slider edit (the Navigator repeats held directions much faster)
        private const float REPEAT_STEP_INTERVAL = 0.15f;
        private const float REPEAT_STEP_SIZE = 10f;

        public Color OldColor { get; private set; }

        private Color _newColor;
        public Color NewColor
        {
            get => _newColor;
            private set
            {
                // The RGB/HSV machinery is alpha-free; transparency is
                // carried separately in _alpha (edited via the A slider)
                value.a = 1f;

                _newColor = value;
                UpdateColorImages();
            }
        }

        private bool _allowTransparency;
        private float _alpha = 1f;
        private ValueSlider _alphaSlider;

        private Action _finishSliderEdit;
        private float _lastRepeatStepTime;

        [SerializeField]
        private Image _oldColorImage;
        [SerializeField]
        private Image _newColorImage;

        [Space]
        [SerializeField]
        private List<TypeSliderPair> _sliders;
        [SerializeField]
        private TMP_InputField _inputField;

        public Action<Color> ColorPickAction;

        private readonly Dictionary<SliderType, ValueSlider> _sliderDict = new();

        private void Awake()
        {
            // Convert slider list into dictionary
            _sliderDict.Clear();
            foreach (var sliderPair in _sliders)
            {
                _sliderDict.Add(sliderPair.Type, sliderPair.Slider);
            }
        }

        // Deliberately no Green/Red entries at the browse level: confirm/cancel
        // must be reached by navigating to the Apply/Cancel buttons. A direct
        // cancel action would be one accidental press away from discarding all
        // edits after backing out of a slider edit (see InitializeNavigation).
        // Targets this dialog's own navigation group explicitly — the dialog
        // prefab's group has _canBeCurrent off, so CurrentNavigationGroup would
        // still be the settings list behind the dialog.
        protected override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Up",
                    ctx => NavigationGroup.SelectPrevious(ctx.IsRepeat)),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Down",
                    ctx => NavigationGroup.SelectNext(ctx.IsRepeat)),
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm",
                    () => NavigationGroup.ConfirmSelection()),
            }, null);
        }

        public void Initialize(Color initialColor, bool allowTransparency)
        {
            OldColor = initialColor;
            NewColor = initialColor;

            _allowTransparency = allowTransparency;
            _alpha = allowTransparency ? initialColor.a : 1f;

            if (allowTransparency)
            {
                CreateAlphaRow();
            }

            UpdateSliders();
            UpdateTextField();
            UpdateColorImages();
        }

        /// <summary>
        /// Registers controller navigation over the dialog: the slider rows in
        /// visual order (H S V, R G B, A), then the Cancel/Apply buttons. Called
        /// by <see cref="DialogManager"/> after the buttons are added, since
        /// adding them registers their own navigatables in creation order.
        /// A selected slider row is tinted yellow (main-menu selection color);
        /// confirming it starts a value edit (up/down ±1, held ±10, back
        /// finishes the edit).
        /// </summary>
        public void InitializeNavigation()
        {
            var navGroup = NavigationGroup;
            navGroup.ClearNavigatables();

            foreach (var sliderType in new[]
            {
                SliderType.H, SliderType.S, SliderType.V,
                SliderType.R, SliderType.G, SliderType.B,
            })
            {
                AddSliderNavigatable(navGroup, _sliderDict[sliderType]);
            }

            if (_alphaSlider != null)
            {
                AddSliderNavigatable(navGroup, _alphaSlider);
            }

            // The dialog button prefab has no NavigatableUnityButton, so give
            // the Cancel/Apply buttons runtime navigatables (Attach draws a
            // button outline since they contain a Button)
            foreach (var button in DialogButtonContainer.GetComponentsInChildren<ColoredButton>())
            {
                var captured = button;
                navGroup.AddNavigatable(RuntimeNavigatable.Attach(captured.gameObject,
                    () => captured.OnClick.Invoke()));
            }

            navGroup.SelectAt(0);
        }

        private void AddSliderNavigatable(NavigationGroup navGroup, ValueSlider slider)
        {
            // The ValueSlider prefab instance sits inside the row object
            // ("H Slider" etc.), which also holds the label
            var row = slider.transform.parent;

            var rowVisual = new SliderRowVisual(row);

            var nav = row.gameObject.AddComponent<RuntimeNavigatable>();
            nav.ConfirmCallback = () => StartSliderEdit(slider, rowVisual);
            nav.SelectionVisual = rowVisual.SetSelected;

            // Hook mouse drag: select the row and show the editing state
            // while the user drags the handle, then revert to browse-
            // selected on release. Must be on the Slider's GO (not the
            // ValueSlider root) because the Slider component is what
            // receives pointer events.
            var unitySlider = row.GetComponentInChildren<Slider>(true);
            if (unitySlider != null)
            {
                var forwarder = unitySlider.gameObject.GetComponent<SliderPointerForwarder>()
                    ?? unitySlider.gameObject.AddComponent<SliderPointerForwarder>();
                forwarder.OnDown = () =>
                {
                    nav.SetSelected(true, SelectionOrigin.Mouse);
                    rowVisual.SetEditing(true);
                };
                forwarder.OnUp = () => rowVisual.SetEditing(false);
            }

            // Hook text field: clicking into the input should select the row
            // and show editing state (same as dragging). Release EventSystem
            // focus on end-edit so the Navigator resumes keyboard processing.
            // Navigation blocking is centralized in Navigator so this field
            // does not mutate the color picker's multi-scheme stack.
            var inputField = row.GetComponentInChildren<TMP_InputField>(true);
            if (inputField != null)
            {
                inputField.onSelect.AddListener(_ =>
                {
                    nav.SetSelected(true, SelectionOrigin.Mouse);
                    rowVisual.SetEditing(true);
                });
                inputField.onEndEdit.AddListener(_ =>
                {
                    rowVisual.SetEditing(false);
                    // Release Unity's selection so keyboard events route back
                    // to the Navigator instead of the destroyed text field
                    var eventSystem = EventSystem.current;
                    if (eventSystem != null &&
                        !eventSystem.alreadySelecting &&
                        eventSystem.currentSelectedGameObject == inputField.gameObject)
                    {
                        eventSystem.SetSelectedGameObject(null);
                    }
                });
            }

            navGroup.AddNavigatable(nav);
        }

        /// <summary>
        /// Selection states for a slider row. Browse-selected: the letter turns
        /// yellow, the bar darkens and the handle turns mid-grey. Editing: the
        /// bar turns a dimmed yellow and the handle returns to white — so it's
        /// obvious both which row has focus and whether it's being edited.
        /// </summary>
        private class SliderRowVisual
        {
            private const float SELECTED_BAR_DARKEN = 0.7f;
            private const float EDITING_YELLOW_DIM = 0.8f;

            private static readonly Color SELECTED_HANDLE_GREY = new(0.82f, 0.82f, 0.82f, 1f);

            private readonly TextMeshProUGUI _label;
            private readonly Image _fill;
            private readonly Image _handle;
            private readonly Color _labelDefault;
            private readonly Color _fillDefault;
            private readonly Color _handleDefault;

            private bool _selected;
            private bool _editing;

            public SliderRowVisual(Transform row)
            {
                var labelTransform = row.Find("Text");
                if (labelTransform != null)
                {
                    _label = labelTransform.GetComponent<TextMeshProUGUI>();
                }

                var slider = row.GetComponentInChildren<Slider>(true);
                if (slider != null && slider.fillRect != null)
                {
                    _fill = slider.fillRect.GetComponent<Image>();
                }
                if (slider != null && slider.handleRect != null)
                {
                    _handle = slider.handleRect.GetComponent<Image>();
                }

                if (_label != null) _labelDefault = _label.color;
                if (_fill != null) _fillDefault = _fill.color;
                if (_handle != null) _handleDefault = _handle.color;
            }

            public void SetSelected(bool selected)
            {
                _selected = selected;
                if (!selected)
                {
                    _editing = false;
                }

                Apply();
            }

            public void SetEditing(bool editing)
            {
                _editing = editing;
                Apply();
            }

            private void Apply()
            {
                if (_label != null)
                {
                    var color = RuntimeNavigatable.SelectedTextColor;
                    color.a = _labelDefault.a;
                    _label.color = _selected ? color : _labelDefault;
                }

                if (_fill != null)
                {
                    Color color;
                    if (_editing)
                    {
                        color = RuntimeNavigatable.SelectedTextColor * EDITING_YELLOW_DIM;
                    }
                    else if (_selected)
                    {
                        // Desaturated grey at the same brightness as the
                        // previous dim yellow — the switch to saturated
                        // yellow on edit is much more noticeable this way
                        var dim = RuntimeNavigatable.SelectedTextColor * SELECTED_BAR_DARKEN;
                        Color.RGBToHSV(dim, out _, out _, out float v);
                        color = Color.HSVToRGB(0f, 0f, v);
                    }
                    else
                    {
                        color = _fillDefault;
                    }

                    color.a = _fillDefault.a;
                    _fill.color = color;
                }

                if (_handle != null)
                {
                    // Medium-grey handle — brighter than the grey bar but
                    // not pure white. Back to white while editing.
                    if (_selected && !_editing)
                    {
                        var color = SELECTED_HANDLE_GREY;
                        color.a = _handleDefault.a;
                        _handle.color = color;
                    }
                    else
                    {
                        _handle.color = _handleDefault;
                    }
                }
            }
        }

        private void StartSliderEdit(ValueSlider slider, SliderRowVisual rowVisual)
        {
            _lastRepeatStepTime = 0f;

            Navigator.Instance.PushSchemeImmediate(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Increase",
                    ctx => AdjustSlider(slider, 1f, ctx.IsRepeat)),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Decrease",
                    ctx => AdjustSlider(slider, -1f, ctx.IsRepeat)),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back",
                    () => Navigator.Instance.PopScheme()),
            }, null, () =>
            {
                // Runs on every pop path (back, dialog closing mid-edit)
                _finishSliderEdit = null;
                rowVisual.SetEditing(false);
            }));

            rowVisual.SetEditing(true);
            _finishSliderEdit = () => Navigator.Instance.PopScheme();
        }

        private void AdjustSlider(ValueSlider slider, float direction, bool isRepeat)
        {
            if (isRepeat)
            {
                // Held direction: bigger steps, throttled to a readable pace
                if (Time.unscaledTime - _lastRepeatStepTime < REPEAT_STEP_INTERVAL)
                {
                    return;
                }

                _lastRepeatStepTime = Time.unscaledTime;
                slider.Value += direction * REPEAT_STEP_SIZE;
            }
            else
            {
                _lastRepeatStepTime = Time.unscaledTime;
                slider.Value += direction;
            }
        }

        protected override void OnBeforeClose()
        {
            // If the dialog is closed (e.g. by mouse) while a slider edit
            // scheme is still active, pop it — OnDisable only pops the
            // dialog's own scheme.
            _finishSliderEdit?.Invoke();
        }

        /// <summary>
        /// Clones the B slider row (plus a block spacer) into an "A" row that
        /// edits transparency on a 0–100 scale, matching the opacity field in
        /// the settings row. Done at runtime so the prefab stays untouched and
        /// non-transparency color fields keep the stock dialog.
        /// </summary>
        private void CreateAlphaRow()
        {
            var bRow = _sliderDict[SliderType.B].transform.parent;
            Debug.Assert(bRow != null, "B slider row not found in ColorPickerDialog prefab");
            var parent = bRow.parent;
            Debug.Assert(parent != null, "Slider column parent not found in ColorPickerDialog prefab");

            // The block gaps are flexible-height spacers that absorb the
            // column's leftover height — adding a row would consume that
            // leftover and collapse every gap. Lay the dialog out as-is,
            // freeze the spacers at their current height, and grow the
            // dialog by one row + one gap instead.
            Canvas.ForceUpdateCanvases();

            float rowHeight = ((RectTransform) bRow).rect.height;
            float gapHeight = 0f;
            Transform spaceTemplate = null;

            foreach (Transform child in parent)
            {
                if (!child.name.StartsWith("Space")) continue;

                if (spaceTemplate == null)
                {
                    spaceTemplate = child;
                    gapHeight = ((RectTransform) child).rect.height;
                }

                var layoutElement = child.GetComponent<LayoutElement>();
                if (layoutElement != null)
                {
                    layoutElement.flexibleHeight = 0f;
                    layoutElement.preferredHeight = gapHeight;
                }
            }

            var baseRect = (RectTransform) transform.Find("Base");
            Debug.Assert(baseRect != null, "'Base' RectTransform not found in ColorPickerDialog prefab");
            baseRect.sizeDelta += new Vector2(0f, rowHeight + gapHeight);

            int insertIndex = bRow.GetSiblingIndex() + 1;

            if (spaceTemplate != null)
            {
                var spaceClone = Instantiate(spaceTemplate.gameObject, parent);
                spaceClone.transform.SetSiblingIndex(insertIndex++);
            }

            var rowClone = Instantiate(bRow.gameObject, parent);
            rowClone.name = "A Slider";
            rowClone.transform.SetSiblingIndex(insertIndex);

            var label = rowClone.transform.Find("Text");
            if (label != null)
            {
                label.GetComponent<TextMeshProUGUI>().text = "A";
            }

            _alphaSlider = rowClone.GetComponentInChildren<ValueSlider>();

            // Drop the cloned persistent OnSliderChanged(B) binding
            _alphaSlider.ValueChanged = new UnityEvent<float>();
            _alphaSlider.ValueChanged.AddListener(OnAlphaChanged);

            _alphaSlider.MinimumValue = 0f;
            _alphaSlider.MaximumValue = 100f;
            _alphaSlider.SetValueWithoutNotify(Mathf.Round(_alpha * 100f));
        }

        private void OnAlphaChanged(float value)
        {
            _alpha = value / 100f;
            UpdateColorImages();
        }

        private void UpdateSliders()
        {
            // Get color components
            var color = NewColor;
            Color.RGBToHSV(color, out float h, out float s, out float v);

            // Set HSV sliders
            _sliderDict[SliderType.H].SetValueWithoutNotify(h * 255f);
            _sliderDict[SliderType.S].SetValueWithoutNotify(s * 255f);
            _sliderDict[SliderType.V].SetValueWithoutNotify(v * 255f);

            // Set RGB sliders
            _sliderDict[SliderType.R].SetValueWithoutNotify(color.r * 255f);
            _sliderDict[SliderType.G].SetValueWithoutNotify(color.g * 255f);
            _sliderDict[SliderType.B].SetValueWithoutNotify(color.b * 255f);
        }

        private void UpdateTextField()
        {
            _inputField.text = ColorUtility.ToHtmlStringRGB(NewColor);
        }

        private void UpdateColorImages()
        {
            _oldColorImage.color = OldColor;

            var newColor = NewColor;
            newColor.a = _alpha;
            _newColorImage.color = newColor;
        }

        // Unity can't have enums in actions lol
        public void OnSliderChanged(int sliderTypeIndex)
        {
            var sliderType = (SliderType) sliderTypeIndex;

            var color = NewColor;
            Color.RGBToHSV(color, out float h, out float s, out float v);

            switch (sliderType)
            {
                case SliderType.H:
                    h = _sliderDict[sliderType].Value / 255f;
                    break;
                case SliderType.S:
                    s = _sliderDict[sliderType].Value / 255f;
                    break;
                case SliderType.V:
                    v = _sliderDict[sliderType].Value / 255f;
                    break;
                case SliderType.R:
                    color.r = _sliderDict[sliderType].Value / 255f;
                    break;
                case SliderType.G:
                    color.g = _sliderDict[sliderType].Value / 255f;
                    break;
                case SliderType.B:
                    color.b = _sliderDict[sliderType].Value / 255f;
                    break;
                default:
                    throw new Exception("Unreachable.");
            }

            if (sliderType is >= SliderType.H and <= SliderType.V)
            {
                // Update from HSV
                NewColor = Color.HSVToRGB(h, s, v);
            }
            else
            {
                // Update from RGB
                NewColor = color;
            }

            UpdateSliders();
            UpdateTextField();
        }

        public void OnTextFieldChanged()
        {
            // Unity needs a hashtag here, but doesn't put it in when converting to string
            if (ColorUtility.TryParseHtmlString("#" + _inputField.text, out var color))
            {
                NewColor = color;
            }

            UpdateSliders();
            UpdateTextField();
        }

        public override void Submit()
        {
            var color = NewColor;
            color.a = _allowTransparency ? _alpha : 1f;

            ColorPickAction?.Invoke(color);

            DialogManager.Instance.ClearDialog();
        }
    }

    /// <summary>
    /// Forwards pointer down/up events from a Unity Slider to arbitrary
    /// callbacks. Added to the Slider's GO (not the parent) so it fires
    /// alongside the Slider's own event handlers.
    /// </summary>
    public class SliderPointerForwarder : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action OnDown;
        public Action OnUp;

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            OnUp?.Invoke();
        }
    }
}

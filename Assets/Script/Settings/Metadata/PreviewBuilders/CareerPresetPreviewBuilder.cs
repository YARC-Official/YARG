using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Menu.Settings.Visuals;
using YARG.Settings.Types;
using Object = UnityEngine.Object;

namespace YARG.Settings.Metadata
{
    public sealed class CareerPresetPreviewBuilder : IPreviewBuilder
    {
        public readonly struct Field
        {
            public readonly string Label;
            public readonly ISettingType Setting;
            public readonly bool Editable;
            public readonly bool Visible;
            public readonly Action ButtonAction;
            public readonly Color? ButtonColor;

            public bool IsButton => ButtonAction != null;

            public Field(string label, ISettingType setting, bool editable = true, bool visible = true)
            {
                Label = label;
                Setting = setting;
                Editable = editable;
                Visible = visible;
                ButtonAction = null;
                ButtonColor = null;
            }

            public Field(string buttonLabel, Action buttonAction, Color? buttonColor = null)
            {
                Label = buttonLabel;
                Setting = null;
                Editable = true;
                Visible = true;
                ButtonAction = buttonAction;
                ButtonColor = buttonColor;
            }
        }

        private IReadOnlyList<Field> _fields = Array.Empty<Field>();
        private Transform _content;
        private ScrollRect _scrollRect;
        private NavigationGroup _navigationGroup;
        private readonly Dictionary<string, GameObject> _fieldObjects = new();
        private Action _onInputBlur;

        public UniTask BuildPreviewWorld(Transform worldContainer) => UniTask.CompletedTask;

        public UniTask BuildPreviewUI(Transform uiContainer)
        {
            var root = new GameObject("Career Editor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(ScrollRect), typeof(NavigationGroup));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(uiContainer, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var background = root.GetComponent<Image>();
            background.color = new Color(0f, 0f, 13f / 255f, 1f);
            background.raycastTarget = false;

            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.SetParent(root.transform, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var contentObject = new GameObject("Settings", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            var contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.SetParent(viewportObject.transform, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            // Setting prefabs define their row height on the RectTransform (84px)
            // rather than through an ILayoutElement preferred height. Controlling
            // child height would therefore resolve every row to zero and stack
            // them all at the top.
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = root.GetComponent<ScrollRect>();
            _scrollRect = scrollRect;
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 10f;

            if (SettingsMenu.Instance?.SettingsScrollbar != null)
            {
                var scrollbar = Object.Instantiate(SettingsMenu.Instance.SettingsScrollbar, root.transform);
                scrollbar.name = "Scrollbar";
                scrollbar.onValueChanged.RemoveAllListeners();
                scrollbar.gameObject.SetActive(true);
                scrollRect.verticalScrollbar = scrollbar;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
                scrollRect.verticalScrollbarSpacing = -3f;
            }

            _content = contentObject.transform;
            _navigationGroup = root.GetComponent<NavigationGroup>();
            root.AddComponent<ScrollViewNavigationUpdater>();
            Render();
            return UniTask.CompletedTask;
        }

        public void Show(IReadOnlyList<Field> fields, int? selectedIndex = null, Action onInputBlur = null)
        {
            _fields = fields ?? Array.Empty<Field>();
            _onInputBlur = onInputBlur;
            Render();

            if (selectedIndex.HasValue && _navigationGroup != null && _navigationGroup.Count > 0)
            {
                _navigationGroup.SelectAt(Mathf.Clamp(selectedIndex.Value, 0, _navigationGroup.Count - 1));
            }
        }

        public void FocusSettings()
        {
            if (_navigationGroup == null || _navigationGroup.Count == 0) return;
            _navigationGroup.SelectFirst(SelectionOrigin.Navigation);
            _navigationGroup.PushNavGroupToStack();
        }

        public void FocusFirstDropdownAndOpen()
        {
            FocusSettings();
            UniTask.NextFrame().ContinueWith(() =>
            {
                if (_navigationGroup?.Navigatables.Count > 0)
                {
                    var dropdown = _navigationGroup.Navigatables[0]
                        .GetComponentInChildren<TMP_Dropdown>(true);
                    RuntimeNavigatable.OpenDropdownList(dropdown);
                }
            }).Forget();
        }

        public void SetFieldVisible(string label, bool visible)
        {
            if (_fieldObjects.TryGetValue(label, out var fieldObject))
            {
                fieldObject.SetActive(visible);

                if (visible && _scrollRect != null && _content is RectTransform contentRect)
                {
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
                    _scrollRect.verticalNormalizedPosition = 0f;
                }
            }
        }

        private void Render()
        {
            if (_content == null) return;

            _navigationGroup.ClearNavigatables();
            _fieldObjects.Clear();
            foreach (Transform child in _content)
            {
                child.gameObject.SetActive(false);
                Object.Destroy(child.gameObject);
            }

            int index = 0;
            foreach (var field in _fields)
            {
                if (field.IsButton)
                {
                    var buttonPrefab = Addressables.LoadAssetAsync<GameObject>("SettingTab/Button")
                        .WaitForCompletion();
                    var buttonObject = Object.Instantiate(buttonPrefab, _content);
                    var buttonRow = buttonObject.GetComponent<SettingsButton>();
                    buttonRow.SetCustomButtons(new[]
                    {
                        new SettingsButton.CustomButton(field.Label, field.ButtonAction),
                    }, backgroundColor: field.ButtonColor);

                    if (field.ButtonColor is { } color && color.r > 0.7f && color.g < 0.4f)
                    {
                        foreach (var text in buttonObject.GetComponentsInChildren<TMPro.TMP_Text>(true))
                        {
                            text.color = Color.white;
                        }
                    }
                    _navigationGroup.AddNavigatable(buttonRow);
                    continue;
                }

                var prefab = Addressables.LoadAssetAsync<GameObject>(field.Setting.AddressableName)
                    .WaitForCompletion();
                var go = Object.Instantiate(prefab, _content);
                _fieldObjects[field.Label] = go;
                var visual = go.GetComponent<BaseSettingVisual>();
                visual.AssignPresetSetting("CareerEditor.Field", hasDescription: false, field.Setting);
                visual.SettingLabel.text = field.Label;
                ApplyCareerInputWidth(go, field.Setting);
                visual.AssignIndex(index++);
                visual.SetEditable(field.Editable, dim: false);
                foreach (var input in go.GetComponentsInChildren<TMP_InputField>(true))
                {
                    var blurCommitter = input.gameObject.AddComponent<CareerInputBlurCommitter>();
                    blurCommitter.Initialize(input, _onInputBlur);
                }
                _navigationGroup.AddNavigatable(go);
                go.SetActive(field.Visible);
            }
        }

        private static void ApplyCareerInputWidth(GameObject row, ISettingType setting)
        {
            float? width = setting switch
            {
                StringSetting               => 0.6f,
                ISearchableDropdownSetting  => 0.6f,
                IDropdownSetting            => 0.4f,
                IntSetting                  => 0.2f,
                _                           => null,
            };

            if (!width.HasValue || row.transform.Find("Container") is not RectTransform container)
            {
                return;
            }

            // Labeled setting controls are right-anchored. Moving only the left
            // anchor makes their width proportional to the complete row while
            // retaining the prefab's authored 25px right inset.
            container.anchorMin = new Vector2(1f - width.Value, container.anchorMin.y);

            if (setting is StringSetting && container.Find("InputField") is RectTransform inputField)
            {
                // StringSetting normally uses only the right half of its container.
                // Career fields size the container itself, so the visible input must
                // fill it or the requested 60% is reduced to 30% of the row.
                inputField.anchorMin = new Vector2(0f, inputField.anchorMin.y);
                inputField.anchorMax = new Vector2(1f, inputField.anchorMax.y);
                inputField.offsetMin = new Vector2(0f, inputField.offsetMin.y);
                inputField.offsetMax = new Vector2(0f, inputField.offsetMax.y);
            }
        }
    }

    /// <summary>
    /// Updates the career summary only after an edited input loses focus. This
    /// keeps typing inexpensive and avoids rebuilding the editor mid-entry.
    /// </summary>
    internal sealed class CareerInputBlurCommitter : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private TMP_InputField _input;
        private Action _onChangedBlur;
        private string _valueOnFocus;
        private bool _selected;

        public void Initialize(TMP_InputField input, Action onChangedBlur)
        {
            _input = input;
            _onChangedBlur = onChangedBlur;
        }

        public void OnSelect(BaseEventData eventData)
        {
            _valueOnFocus = _input?.text ?? string.Empty;
            _selected = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (!_selected) return;

            _selected = false;
            if (_input != null && !string.Equals(_valueOnFocus, _input.text, StringComparison.Ordinal))
            {
                _onChangedBlur?.Invoke();
            }
        }
    }
}

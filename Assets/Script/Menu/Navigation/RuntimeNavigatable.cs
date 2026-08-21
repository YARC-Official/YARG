using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YARG.Core.Input;

namespace YARG.Menu.Navigation
{
    /// <summary>
    /// A <see cref="NavigatableBehaviour"/> that can be added to a GameObject at
    /// runtime. The base class requires a serialized "selected visual" GameObject
    /// that only prefab-authored navigatables can provide (its Awake throws on
    /// null); this variant instead drives a translucent overlay created by
    /// <see cref="Attach"/>, and invokes a callback on confirm.
    /// </summary>
    public class RuntimeNavigatable : NavigatableBehaviour
    {
        /// <summary>
        /// The selected-text yellow used by the main menu's
        /// NavigationTextColorizer (serialized in MainMenu.prefab).
        /// </summary>
        public static readonly Color SelectedTextColor = new(1f, 0.83137256f, 0.22745098f, 1f);

        private static Sprite _selectionGradient;
        private static Sprite _buttonOutline;

        private static Sprite GetSelectionGradient()
        {
            if (_selectionGradient == null)
            {
                _selectionGradient = UnityEngine.AddressableAssets.Addressables
                    .LoadAssetAsync<Sprite>("SelectionGradient")
                    .WaitForCompletion();
            }
            return _selectionGradient;
        }

        private static Sprite GetButtonOutline()
        {
            if (_buttonOutline == null)
            {
                _buttonOutline = UnityEngine.AddressableAssets.Addressables
                    .LoadAssetAsync<Sprite>("ButtonOutline")
                    .WaitForCompletion();
            }
            return _buttonOutline;
        }

        /// <summary>
        /// Creates an inactive selection outline around <paramref name="parent"/>
        /// in the given color, using RoundButton's authored "Selection Outline"
        /// sprite and oversize.
        /// </summary>
        public static GameObject CreateSelectionOutline(Transform parent, Color color)
        {
            var outline = new GameObject("SelectedOutline", typeof(RectTransform));
            var outlineRect = outline.GetComponent<RectTransform>();
            outlineRect.SetParent(parent, false);
            outlineRect.anchorMin = Vector2.zero;
            outlineRect.anchorMax = Vector2.one;
            outlineRect.anchoredPosition = Vector2.zero;
            outlineRect.sizeDelta = new Vector2(12f, 12f);

            var outlineImage = outline.AddComponent<Image>();
            outlineImage.sprite = GetButtonOutline();
            outlineImage.type = Image.Type.Sliced;
            outlineImage.color = color;
            outlineImage.raycastTarget = false;

            outline.SetActive(false);
            return outline;
        }

        /// <summary>
        /// The rect a focus outline should hug for a button root: SmallRoundButton-
        /// based prefabs have an oversized root around a 32-tall visible "Button"
        /// pill, so the outline hugs the visible <see cref="Button"/> child when
        /// one exists, otherwise the passed-in component's own transform.
        /// </summary>
        public static Transform GetButtonOutlineTarget(Component button)
        {
            var visible = button.GetComponentInChildren<Button>(true);
            return visible != null ? visible.transform : button.transform;
        }

        // The authored outline's green, for the default button/dropdown visuals
        private static GameObject CreateOutline(Transform parent)
        {
            return CreateSelectionOutline(parent, new Color(0.03529412f, 0.87058824f, 0.48235294f));
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }

                var found = FindDeepChild(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public Action ConfirmCallback;
        public Action<bool> SelectionVisual;

        // Don't call base.Awake — it accesses the null _selectedVisual field.
        protected override void Awake()
        {
            // Bridge child-button clicks to keyboard-nav selection. Unity's
            // pointer-down event stops at the first IPointerDownHandler (the
            // child Button via Selectable), so the parent row's navigatable
            // never receives it. This listener fires on click (pointer-up),
            // after the button's own onClick action (e.g. opening a dialog),
            // so the row is selected in the same frame the dialog opens.
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                button.onClick.AddListener(() => SetSelected(true, SelectionOrigin.Mouse));
            }

            // TMP_Dropdown is a Selectable, not a Button — there's no onClick
            // to hook. Add a pointer-down forwarder on the dropdown's own
            // GameObject. Execute calls every IPointerDownHandler on that
            // GameObject, so both the Selectable and our forwarder fire.
            foreach (var dropdown in GetComponentsInChildren<TMP_Dropdown>(true))
            {
                var forwarder = dropdown.gameObject.AddComponent<DropdownClickForwarder>();
                forwarder.Target = this;
            }
        }

        protected override void OnSelectionChanged(bool selected)
        {
            SelectionVisual?.Invoke(selected);
        }

        public override void Confirm()
        {
            ConfirmCallback?.Invoke();
        }

        /// <summary>
        /// Adds a RuntimeNavigatable to <paramref name="go"/> with a selection
        /// indicator matching what the game uses for the widget type: a
        /// prefab-authored visual if one exists ("Selected Background" on
        /// setting rows, "Selection Outline" on buttons), the button outline
        /// drawn around the dropdown box for bare dropdown rows (whose box
        /// would hide a row overlay), or a stretched overlay replicating the
        /// setting row highlight (SelectionGradient at 0.05 alpha) otherwise.
        /// </summary>
        public static RuntimeNavigatable Attach(GameObject go, Action confirm)
        {
            var nav = go.AddComponent<RuntimeNavigatable>();
            nav.ConfirmCallback = confirm;

            var authored = FindDeepChild(go.transform, "Selected Background")
                ?? FindDeepChild(go.transform, "Selection Outline");
            if (authored != null)
            {
                authored.gameObject.SetActive(false);
                nav.SelectionVisual = authored.gameObject.SetActive;
                return nav;
            }

            var dropdown = go.GetComponentInChildren<TMP_Dropdown>(true);
            if (dropdown != null)
            {
                var outline = CreateOutline(dropdown.transform);
                nav.SelectionVisual = outline.SetActive;
                return nav;
            }

            // Buttons without an authored outline (e.g. dialog buttons based on
            // SmallRoundButton) get a runtime one — an overlay would be
            // invisible on top of the colored button image. The outline hugs
            // the Button's own rect: SmallRoundButton-style roots are oversized
            // around the visible pill.
            var button = go.GetComponentInChildren<Button>(true);
            if (button != null)
            {
                var outline = CreateOutline(GetButtonOutlineTarget(go.transform));
                nav.SelectionVisual = outline.SetActive;
                return nav;
            }

            var overlay = new GameObject("SelectedOverlay", typeof(RectTransform));
            var rect = overlay.GetComponent<RectTransform>();
            rect.SetParent(go.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            // Stock rows draw the highlight behind the row content
            rect.SetSiblingIndex(0);

            // Keep layout-group parents from repositioning the overlay
            var layoutElement = overlay.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            var image = overlay.AddComponent<Image>();
            image.sprite = GetSelectionGradient();
            image.color = new Color(1f, 1f, 1f, 0.050980393f);
            image.raycastTarget = false;

            overlay.SetActive(false);
            nav.SelectionVisual = overlay.SetActive;

            return nav;
        }

        /// <summary>
        /// Adds a RuntimeNavigatable to <paramref name="go"/> that indicates
        /// selection by tinting text yellow, like the main menu entries do
        /// (main-menu style: no highlight rectangle). Tints the given labels,
        /// or every TMP under <paramref name="go"/> when none are passed.
        /// </summary>
        public static RuntimeNavigatable AttachTextHighlight(GameObject go, Action confirm,
            params TMP_Text[] texts)
        {
            var nav = go.AddComponent<RuntimeNavigatable>();
            nav.ConfirmCallback = confirm;

            var targets = texts is { Length: > 0 }
                ? texts
                : go.GetComponentsInChildren<TextMeshProUGUI>(true);

            var defaults = new Color[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                defaults[i] = targets[i].color;
            }

            nav.SelectionVisual = selected =>
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    if (targets[i] == null) continue;

                    if (selected)
                    {
                        var color = SelectedTextColor;
                        color.a = defaults[i].a;
                        targets[i].color = color;
                    }
                    else
                    {
                        targets[i].color = defaults[i];
                    }
                }
            };

            return nav;
        }

        /// <summary>
        /// Opens a TMP dropdown's list and navigates it with controller input:
        /// Up/Down move the highlighted item (via EventSystem selection, which
        /// drives the item's normal selected tint), Confirm picks it (firing the
        /// dropdown's regular change handling), Back/Escape cancels. The pushed
        /// scheme pops itself when the list closes by any path — pick, cancel, or
        /// clicking the blocker — via a watcher on the list object.
        /// </summary>
        public static void OpenDropdownList(TMP_Dropdown dropdown)
        {
            if (dropdown == null || dropdown.options.Count == 0)
            {
                return;
            }

            // Open the list unless TMP already opened it (e.g. via its native
            // OnPointerClick from a mouse click). Calling Show() twice creates
            // duplicate lists, so check for the existing child first.
            if (dropdown.transform.Find("Dropdown List") == null)
            {
                dropdown.Show();
            }

            // TMP instantiates its template as a child named "Dropdown List"
            var list = dropdown.transform.Find("Dropdown List");
            if (list == null)
            {
                return;
            }

            var toggles = list.GetComponentsInChildren<Toggle>();
            if (toggles.Length == 0)
            {
                return;
            }

            var scrollRect = list.GetComponent<ScrollRect>();
            int index = Mathf.Clamp(dropdown.value, 0, toggles.Length - 1);

            void Highlight()
            {
                var eventSystem = EventSystem.current;
                if (eventSystem != null)
                {
                    eventSystem.SetSelectedGameObject(toggles[index].gameObject);
                }

                // Keep the highlighted item in view (items are uniform height)
                if (scrollRect != null && toggles.Length > 1)
                {
                    scrollRect.verticalNormalizedPosition =
                        1f - (float) index / (toggles.Length - 1);
                }
            }

            Highlight();

            bool popped = false;
            void PopOnce()
            {
                // Navigator can already be gone if the list dies during teardown
                if (!popped && Navigator.Instance != null)
                {
                    popped = true;
                    Navigator.Instance.PopScheme();
                }
            }

            var watcher = list.gameObject.AddComponent<DropdownListCloseWatcher>();
            watcher.Closed = PopOnce;

            if (Navigator.Instance == null) return;

            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Previous", () =>
                {
                    if (dropdown == null) { PopOnce(); return; }
                    index = (index - 1 + toggles.Length) % toggles.Length;
                    Highlight();
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Next", () =>
                {
                    if (dropdown == null) { PopOnce(); return; }
                    index = (index + 1) % toggles.Length;
                    Highlight();
                }),
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm", () =>
                {
                    if (dropdown == null) { PopOnce(); return; }
                    if (index == dropdown.value)
                    {
                        // TMP suppresses the change event for the same index, so
                        // the list wouldn't close; treat re-picking as a cancel.
                        dropdown.Hide();
                    }
                    else
                    {
                        toggles[index].isOn = true;
                    }
                }),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Cancel", () =>
                {
                    if (dropdown == null) { PopOnce(); return; }
                    dropdown.Hide();
                }),
            }, null));
        }
    }

    /// <summary>
    /// Pops the dropdown-list navigation scheme when the TMP list object is
    /// destroyed (TMP destroys it on every close path). Added at runtime by
    /// <see cref="RuntimeNavigatable.OpenDropdownList"/>.
    /// </summary>
    public class DropdownListCloseWatcher : MonoBehaviour
    {
        public Action Closed;

        private void OnDestroy()
        {
            Closed?.Invoke();
        }
    }

    /// <summary>
    /// Forwards pointer events from a child TMP_Dropdown (a Selectable
    /// with no onClick) to the parent RuntimeNavigatable. On pointer-down,
    /// selects the parent row. On pointer-click (after TMP's own
    /// OnPointerClick has opened the list natively), defers one frame then
    /// calls <see cref="RuntimeNavigatable.OpenDropdownList"/> to attach
    /// keyboard/controller navigation to the already-open list.
    /// </summary>
    public class DropdownClickForwarder : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        public NavigatableBehaviour Target;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Target != null)
            {
                Target.SetSelected(true, SelectionOrigin.Mouse);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // TMP's OnPointerClick (also on this GameObject) already called
            // Show() and opened the native list. Defer to next frame so we
            // can detect the open list and attach keyboard navigation to it
            // without re-calling Show().
            var dropdown = GetComponent<TMP_Dropdown>();
            if (dropdown == null) return;

            UniTask.NextFrame().ContinueWith(() =>
            {
                if (dropdown != null && dropdown.transform.Find("Dropdown List") != null)
                {
                    RuntimeNavigatable.OpenDropdownList(dropdown);
                }
            }).Forget();
        }
    }
}

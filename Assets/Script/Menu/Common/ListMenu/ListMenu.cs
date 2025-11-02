using System;
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
                if (_viewList == null || _viewList.Count == 0)
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
            _viewObjectParent = ResolveTransformReference(_viewObjectParent, "view object parent", fallbackToSelf: true);
            _scrollbar = ResolveComponentReference(_scrollbar, "scrollbar");
            _viewAligner = ResolveComponentReference(_viewAligner, "view aligner");

            if (_viewObjectPrefab == null)
            {
                Debug.LogError($"[{GetType().Name}] View object prefab is not assigned on {name}.", this);
                return;
            }

            var viewParent = _viewObjectParent != null ? _viewObjectParent : transform;

            // Create all of the replay views
            for (int i = 0; i < ExtraListViewPadding * 2 + 1; i++)
            {
                // Instantiate with worldPositionStays = false so the prefab's local transform is preserved
                // and explicitly reset localScale to avoid inherited canvas scaling issues.
                var instance = Instantiate(_viewObjectPrefab, viewParent, false);
                // Ensure local scale is normalized
                instance.transform.localScale = Vector3.one;

                // Grab RectTransform to use its vertical size for preferredHeight if available.
                // Important: do NOT override the prefab's horizontal anchors or width here —
                // the prefab should drive horizontal layout. Overriding sizeDelta.x to 0
                // was causing instances to render with zero width in some layouts.
                var rt = instance.GetComponent<RectTransform>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Debug log to help trace width/anchor issues when items appear to collapse to zero width.
                try
                {
                    var parentName = viewParent != null ? viewParent.name : "null";
                    var parentRt = viewParent as RectTransform;
                    var parentInfo = parentRt != null ? $"parent.name={parentName} parent.size=({parentRt.rect.width:F1},{parentRt.rect.height:F1})" : $"parent.name={parentName} parent=null";
                        string rtInfo = rt != null ? $"anchorMin=({rt.anchorMin.x:F2},{rt.anchorMin.y:F2}) anchorMax=({rt.anchorMax.x:F2},{rt.anchorMax.y:F2}) size=({rt.sizeDelta.x:F1},{rt.sizeDelta.y:F1})" : "rt=null";
                        var vlg = viewParent != null ? viewParent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>() : null;
                        string vlgInfo = vlg != null ? $"VLG(childControlWidth={vlg.childControlWidth},forceExpandWidth={vlg.childForceExpandWidth})" : "VLG=null";

                        // Build ancestor chain info for diagnosis
                        string ancestors = "";
                        var t = viewParent;
                        int depth = 0;
                        while (t != null && depth < 8)
                        {
                            var tr = t as RectTransform;
                            if (tr != null)
                            {
                                ancestors += $"/{t.name}(aMin={tr.anchorMin.x:F2},aMax={tr.anchorMax.x:F2},w={tr.rect.width:F1})";
                            }
                            else
                            {
                                ancestors += $"/{t.name}(noRT)";
                            }
                            t = t.parent;
                            depth++;
                        }

                        Debug.Log($"[ListMenu] Instantiated view '{_viewObjectPrefab.name}' -> {rtInfo} ; {parentInfo} ; {vlgInfo} ; ancestors={ancestors}");
                }
                catch { }
#endif

                // Ensure there is a LayoutElement so parent layout groups know preferred height
                var layoutElem = instance.GetComponent<UnityEngine.UI.LayoutElement>();
                if (layoutElem == null)
                {
                    layoutElem = instance.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                }
                // Use the prefab's height as preferred height if available
                if (rt != null && rt.sizeDelta.y > 0f)
                {
                    layoutElem.preferredHeight = rt.sizeDelta.y;
                }

                var view = instance as TViewObject;
                if (view == null)
                {
                    Debug.LogError($"[{GetType().Name}] Instantiated view prefab does not contain component {typeof(TViewObject).Name}.", instance.gameObject);
                    Destroy(instance.gameObject);
                    continue;
                }
                // If the instantiated RectTransform has a non-positive horizontal size (or zero),
                // try to recover a valid width from an ancestor or the canvas so the item doesn't collapse.
                if (rt != null && rt.sizeDelta.x <= 0f)
                {
                    float recovered = 0f;
                    var t = viewParent as RectTransform;
                    while (t != null)
                    {
                        if (t.rect.width > 1f)
                        {
                            recovered = t.rect.width;
                            break;
                        }
                        t = t.parent as RectTransform;
                    }

                    if (recovered <= 1f)
                    {
                        // Try canvas root
                        var canvas = instance.GetComponentInParent<Canvas>();
                        if (canvas != null && canvas.pixelRect.width > 1f)
                            recovered = canvas.pixelRect.width;
                    }

                    if (recovered > 1f)
                    {
                        rt.sizeDelta = new Vector2(recovered, rt.sizeDelta.y);
                        Debug.Log($"[{GetType().Name}] Recovered width for instantiated view '{_viewObjectPrefab.name}' = {recovered:F1}");
                    }
                }

                _viewObjects.Add(view);

                if (i == ExtraListViewPadding && _viewAligner != null)
                {
                    _viewAligner.SelectedView = instance.GetComponent<RectTransform>();
                }
            }

            RequestViewListUpdate();
        }

        private Transform ResolveTransformReference(Transform reference, string fieldName, bool fallbackToSelf)
        {
            if (reference == null)
            {
                if (fallbackToSelf)
                {
                    Debug.LogError($"[{GetType().Name}] {fieldName} is not assigned on {name}; defaulting to self.", this);
                    return transform;
                }

                Debug.LogWarning($"[{GetType().Name}] {fieldName} is not assigned on {name}; leaving unset.", this);
                return null;
            }

            if (reference.gameObject.scene.IsValid())
            {
                return reference;
            }

            string path = BuildTransformPath(reference);
            Transform runtime = string.IsNullOrEmpty(path) ? transform : transform.Find(path);
            if (runtime != null)
            {
                return runtime;
            }

            if (fallbackToSelf)
            {
                Debug.LogWarning($"[{GetType().Name}] Could not resolve runtime transform for '{reference.name}' on {name}; defaulting to self.", this);
                return transform;
            }

            Debug.LogWarning($"[{GetType().Name}] Could not resolve runtime transform for '{reference.name}' on {name}; leaving unset.", this);
            return null;
        }

        private T ResolveComponentReference<T>(T reference, string fieldName) where T : Component
        {
            if (reference == null)
            {
                return null;
            }

            if (reference.gameObject.scene.IsValid())
            {
                return reference;
            }

            string path = BuildTransformPath(reference.transform);
            Transform runtime = string.IsNullOrEmpty(path) ? transform : transform.Find(path);
            if (runtime == null)
            {
                Debug.LogWarning($"[{GetType().Name}] Could not resolve runtime component for '{reference.name}' ({fieldName}) on {name}; leaving unset.", this);
                return null;
            }

            if (!runtime.TryGetComponent(out T resolved))
            {
                Debug.LogWarning($"[{GetType().Name}] Resolved transform '{runtime.name}' does not contain component {typeof(T).Name} for {fieldName} on {name}; leaving unset.", this);
                return null;
            }

            return resolved;
        }

        private string BuildTransformPath(Transform target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            var stack = new Stack<string>();
            var current = target;

            while (current != null && current != transform)
            {
                stack.Push(current.name);
                current = current.parent;
            }

            return stack.Count == 0 ? string.Empty : string.Join("/", stack);
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
            if (_scrollbar == null || _viewList == null || _viewList.Count <= 1)
            {
                return;
            }

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
            if (_scrollbar == null)
            {
                return;
            }

            if (_viewList == null || _viewList.Count <= 1)
            {
                _scrollbar.SetValueWithoutNotify(0f);
                return;
            }

            _scrollbar.SetValueWithoutNotify((float) SelectedIndex / (_viewList.Count - 1));
        }

        protected void RequestViewListUpdate()
        {
            _viewList = CreateViewList() ?? new List<TViewType>();
            RefreshViewsObjects();
        }

        protected abstract List<TViewType> CreateViewList();

        public void RefreshViewsObjects()
        {
            if (_viewObjects.Count == 0)
            {
                return;
            }

            if (_viewList == null || _viewList.Count == 0)
            {
                foreach (var view in _viewObjects)
                {
                    view.Hide();
                }

                return;
            }

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
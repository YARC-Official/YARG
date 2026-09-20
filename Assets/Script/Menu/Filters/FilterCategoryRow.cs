using TMPro;
using UnityEngine;
using System;
using YARG.Menu.Navigation;
using UnityEngine.UI;

namespace YARG.Menu.Filters
{
    public readonly struct FilterKey : IEquatable<FilterKey>
    {
        public readonly FilterGroup Group;
        public readonly Guid ContextId;

        public FilterKey(FilterGroup group, Guid contextId = default)
        {
            Group = group;
            ContextId = contextId;
        }

        public bool Equals(FilterKey other)
        {
            return Group == other.Group && ContextId.Equals(other.ContextId);
        }

        public override bool Equals(object obj)
        {
            return obj is FilterKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Group, ContextId);
        }

        public static bool operator ==(FilterKey left, FilterKey right) => left.Equals(right);
        public static bool operator !=(FilterKey left, FilterKey right) => !left.Equals(right);
    }

    public class FilterCategoryRow : NavigatableBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _secondaryLabel;

        public FilterKey Key { get; private set; }
        public FilterGroup Group => Key.Group;

        private FilterRowBackgroundVisual _backgroundVisual;

        protected override void Awake()
        {
            base.Awake();

            _backgroundVisual = GetComponent<FilterRowBackgroundVisual>();
            if (_backgroundVisual == null)
                _backgroundVisual = gameObject.AddComponent<FilterRowBackgroundVisual>();
        }

        public void Init(FilterKey key, string label, string secondaryLabel = null)
        {
            Key = key;
            _label.text = label;
            SetSecondaryText(secondaryLabel);
        }

        public void AssignIndex(int index)
        {
            _backgroundVisual?.AssignIndex(index);
        }

        public void ShowAdvancedMarker()
        {
            var marker = new GameObject("Advanced Setting Marker", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image));
            marker.layer = gameObject.layer;
            marker.transform.SetParent(transform, false);
            marker.transform.SetAsFirstSibling();

            var rect = (RectTransform) marker.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(10f, -2f);
            rect.pivot = new Vector2(0f, 0.5f);

            var image = marker.GetComponent<Image>();
            image.color = new Color(0.29411766f, 0.7490196f, 0.8156863f, 0.11372549f);
            image.raycastTarget = false;
        }

        public void SetSecondaryText(string text)
        {
            if (_secondaryLabel == null) return;

            _secondaryLabel.text = text ?? string.Empty;
            _secondaryLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }

    public enum FilterGroup
    {
        Genre,
        Subgenre,
        Decade,
        VocalParts,
        Source,
        Playlist,
        Charter,
        Intensity,
        Length
    }
}


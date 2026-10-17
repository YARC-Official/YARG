using TMPro;
using UnityEngine;
using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YARG.Menu.Navigation;

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

        public event Action<FilterCategoryRow> Clicked;

        private FilterRowBackgroundVisual _backgroundVisual;
        private Image _selectionBackground;

        protected override void Awake()
        {
            base.Awake();

            _backgroundVisual = GetComponent<FilterRowBackgroundVisual>();
            if (_backgroundVisual == null)
                _backgroundVisual = gameObject.AddComponent<FilterRowBackgroundVisual>();

            // BaseSetting's legacy selection image became transparent in the settings
            // redesign, but this prefab variant still relies on it for controller focus.
            _selectionBackground = transform.Find("Selected Background")?.GetComponent<Image>();
            if (_selectionBackground != null)
            {
                _selectionBackground.sprite = null;
                _selectionBackground.color = new Color(0.035f, 0.3f, 0.42f, 0.9f);
            }
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

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            Clicked?.Invoke(this);
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


using TMPro;
using UnityEngine;
using YARG.Menu.Navigation;

namespace YARG.Menu.Filters
{
    public class FilterCategoryRow : NavigatableBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _secondaryLabel;

        public FilterGroup Filters { get; private set; }

        private FilterRowBackgroundVisual _backgroundVisual;

        protected override void Awake()
        {
            base.Awake();

            _backgroundVisual = GetComponent<FilterRowBackgroundVisual>();
            if (_backgroundVisual == null)
                _backgroundVisual = gameObject.AddComponent<FilterRowBackgroundVisual>();
        }

        public void Init(FilterGroup group, string label, string secondaryLabel = null)
        {
            Filters = group;
            _label.text = label;
            SetSecondaryText(secondaryLabel);
        }

        public void AssignIndex(int index)
        {
            _backgroundVisual?.AssignIndex(index);
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
        Decade,
        VocalParts,
        Source,
        Charter,
        Difficulty,
        Length
    }
}


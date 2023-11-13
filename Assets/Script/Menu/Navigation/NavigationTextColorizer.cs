using TMPro;
using UnityEngine;

namespace YARG.Menu.Navigation
{
    [RequireComponent(typeof(NavigatableBehaviour))]
    public class NavigationTextColorizer : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI[] _texts;

        [SerializeField]
        private Color _selectedColor = Color.white;
        [SerializeField]
        private bool _preserveAlpha = true;

        private Color[] _defaultColors;
        private NavigatableBehaviour _navigatableBehaviour;

        private void Awake()
        {
            _navigatableBehaviour = GetComponent<NavigatableBehaviour>();
            _navigatableBehaviour.SelectionStateChanged += OnSelectionStateChanged;

            // Get the default colors
            _defaultColors = new Color[_texts.Length];
            for (int i = 0; i < _texts.Length; i++)
            {
                _defaultColors[i] = _texts[i].color;
            }

            // Force update the text color
            OnSelectionStateChanged(_navigatableBehaviour.Selected);
        }

        private void OnSelectionStateChanged(bool selected)
        {
            for (int i = 0; i < _texts.Length; i++)
            {
                var defaultColor = _defaultColors[i];
                var selectedColor = _selectedColor;

                if (_preserveAlpha)
                {
                    selectedColor.a = defaultColor.a;
                }

                _texts[i].color = selected ? selectedColor : defaultColor;
            }
        }

        private void OnDestroy()
        {
            _navigatableBehaviour.SelectionStateChanged -= OnSelectionStateChanged;
        }
    }
}
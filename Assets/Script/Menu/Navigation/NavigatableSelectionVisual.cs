using UnityEngine;

namespace YARG.Menu.Navigation
{
    [RequireComponent(typeof(NavigatableBehaviour))]
    public sealed class NavigatableSelectionVisual : MonoBehaviour
    {
        private NavigatableBehaviour _navigatableBehaviour;

        [SerializeField]
        private GameObject[] _selectionVisuals;

        private void Awake()
        {
            _navigatableBehaviour = GetComponent<NavigatableBehaviour>();
            _navigatableBehaviour.SelectionChanged += OnSelectionChanged;

            OnSelectionChanged(false);
        }

        private void OnDestroy()
        {
            _navigatableBehaviour.SelectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged(bool selected)
        {
            foreach (var selectionObject in _selectionVisuals)
            {
                selectionObject.SetActive(selected);
            }
        }
    }
}
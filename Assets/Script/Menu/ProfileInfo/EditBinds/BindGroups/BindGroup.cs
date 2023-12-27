using UnityEngine;
using YARG.Input;
using YARG.Player;

namespace YARG.Menu.ProfileInfo
{
    public abstract class BindGroup<TView, TState, TBinding, TSingle> : MonoBehaviour
        where TView : SingleBindView<TState, TBinding, TSingle>
        where TState : struct
        where TBinding : ControlBinding<TState, TSingle>
        where TSingle : SingleBinding<TState>
    {
        [SerializeField]
        private BindHeader _header;
        [SerializeField]
        private TView _viewPrefab;

        protected TBinding _binding;

        public void Init(EditBindsTab editBindsTab, YargPlayer player, TBinding binding)
        {
            _binding = binding;

            _header.Init(editBindsTab, player, binding);

            _binding.StateChanged += OnStateChanged;
            _binding.BindingsChanged += RefreshBindings;
            RefreshBindings();
            OnStateChanged();
        }

        private void OnDestroy()
        {
            if (_binding != null)
            {
                _binding.StateChanged -= OnStateChanged;
                _binding.BindingsChanged -= RefreshBindings;
            }
        }

        protected abstract void OnStateChanged();

        public void RefreshBindings()
        {
            _header.RefreshBindings<TView, TState, TBinding, TSingle>(_viewPrefab, _binding, _binding.Bindings);
        }
    }
}
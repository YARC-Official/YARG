using UnityEngine;
using YARG.Input.Bindings;

namespace YARG.Menu.ProfileList
{
    public abstract class ReusableBindGroup<TView, TBinding, TSingle> : MonoBehaviour
        where TView: ReusableSingleBindView<TBinding, TSingle>
        where TBinding : ReusableControlBinding<TSingle>
        where TSingle : ReusableSingleBinding
    {
        [SerializeField]
        protected ReusableBindHeader _header;
        [SerializeField]
        protected TView _viewPrefab;

        protected TBinding _binding;

        public virtual void Init(BindingSetsCenterPane centerPane, ReusableBindingSet bindingSet, TBinding binding)
        {
            _binding = binding;

            _header.Init(centerPane, bindingSet, binding);

            RefreshBindings();
        }

        public virtual void RefreshBindings()
        {
            _header.ClearBindings();

            foreach (var control in _binding.Bindings)
            {
                _header.AddBinding<TView, TBinding, TSingle>(_viewPrefab, _binding, control);
            }

            _header.RebuildBindingsLayout();
        }
    }
}

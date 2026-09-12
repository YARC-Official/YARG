using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using YARG.Helpers;
using YARG.Input.Bindings;

namespace YARG.Menu.ProfileList
{
    public abstract class ReusableBindGroup<TSingleView, TBinding, TSingle> : MonoBehaviour
        where TSingleView: ReusableSingleBindView<TBinding, TSingle>
        where TBinding : ReusableControlBinding<TSingle>
        where TSingle : ReusableSingleBinding
    {
        [SerializeField]
        protected ReusableBindHeader _header;
        [SerializeField]
        protected TSingleView _viewPrefab;

        protected TBinding _binding;

        protected List<ControlItemInfo> _controls;

        public virtual void Init(
            BindingSetsCenterPane centerPane,
            ReusableBindingSet bindingSet,
            TBinding binding,
            List<ControlItemInfo> controls
        )
        {
            _binding = binding;
            _controls = controls;

            _header.Init(centerPane, bindingSet, binding);

            RefreshBindings();
        }

        public virtual void RefreshBindings()
        {
            _header.ClearBindings();

            foreach (var control in _binding.Bindings)
            {
                _header.AddBinding<TSingleView, TBinding, TSingle>(_viewPrefab, _binding, control, _controls);
            }

            _header.RebuildBindingsLayout();
        }
    }
}

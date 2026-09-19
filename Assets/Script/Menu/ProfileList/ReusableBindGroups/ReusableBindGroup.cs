using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using YARG.Helpers;
using YARG.Input.Bindings;

namespace YARG.Menu.ProfileList
{
    public abstract class ReusableBindGroup<TSingleView, TBinding, TSingle, TSingleState> : MonoBehaviour
        where TSingleView : ReusableSingleBindView<TBinding, TSingle, TSingleState>
        where TBinding : ReusableControlBinding<TSingle, TSingleState>
        where TSingle : ReusableSingleBinding<TSingleState>
        where TSingleState : struct
    {
        [SerializeField]
        protected ReusableBindHeader _header;
        [SerializeField]
        protected TSingleView _viewPrefab;

        public TBinding Binding { get; protected set; }

        protected List<ControlItemInfo> _controls;

        public virtual void Init(
            BindingSetsCenterPane centerPane,
            ReusableBindingSet bindingSet,
            TBinding binding,
            List<ControlItemInfo> controls
        )
        {
            Binding = binding;
            _controls = controls;

            _header.Init(centerPane, bindingSet, binding);

            RefreshBindings();
        }

        public virtual void RefreshBindings()
        {
            _header.ClearBindings();

            foreach (var control in Binding.Bindings)
            {
                _header.AddBinding<TSingleView, TBinding, TSingle, TSingleState>(_viewPrefab, Binding, control, _controls);
            }

            _header.RebuildBindingsLayout();
        }
    }
}

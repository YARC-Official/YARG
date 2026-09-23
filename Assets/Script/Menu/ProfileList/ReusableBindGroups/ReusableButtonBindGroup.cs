using Minis;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using YARG.Helpers;
using YARG.Input;
using YARG.Input.Bindings;
using YARG.Menu.ProfileInfo;
using YARG.Player;

namespace YARG.Menu.ProfileList
{
    public class ReusableButtonBindGroup
        : ReusableBindGroup<ReusableSingleButtonBindView, ReusableButtonBinding, ReusableSingleButtonBinding, float>
    {
        [SerializeField]
        private SingleMidiNoteBindView _midiNoteViewPrefab;

        [Space]
        [SerializeField]
        private ButtonDisplay _pressedIndicator;

        [Space]
        [SerializeField]
        private ValueSlider _debounceSlider;

        public override void Init(
            BindingSetsCenterPane centerPane,
            ReusableBindingSet bindingSet,
            ReusableButtonBinding binding,
            List<ControlItemInfo> controls
        )
        {
            base.Init(centerPane, bindingSet, binding, controls);

            _debounceSlider.SetValueWithoutNotify(binding.DebounceThreshold);
        }

        public void OnDebounceValueChanged(float value)
        {
            Binding.DebounceThreshold = (long) value;
        }

        public override void RefreshBindings()
        {
            _bindingList.ClearDrawer();

            foreach (var control in Binding.Bindings)
            {
                /* TODO-FRICK: MIDI stuff
                if (control.Control is MidiNoteControl)
                {
                    var bindView = _bindingList.AddNewWithoutRebuild(_midiNoteViewPrefab);
                    bindView.Init(Binding, control, _controls);
                }
                else
                {*/
                    var bindView = _bindingList.AddNewWithoutRebuild(_viewPrefab);
                    bindView.Init(Binding, control, _controls);
                //}

                bindView.DeleteRequested += DeleteBinding;
            }

            _bindingList.RebuildLayout();
        }

        public override void AddNewBinding()
        {
            Binding.Bindings.Add(new());
            RefreshBindings();
        }
    }
}

using Minis;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using YARG.Input;
using YARG.Input.Bindings;
using YARG.Menu.ProfileInfo;
using YARG.Player;

namespace YARG.Menu.ProfileList
{
    public class ReusableButtonBindGroup
        : ReusableBindGroup<ReusableSingleButtonBindView, ReusableButtonBinding, ReusableSingleButtonBinding>
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
            List<InputControlLayout.ControlItem> controls
        )
        {
            base.Init(centerPane, bindingSet, binding, controls);

            _debounceSlider.SetValueWithoutNotify(binding.DebounceThreshold);
        }

        public void OnDebounceValueChanged(float value)
        {
            _binding.DebounceThreshold = (long) value;
        }

        public override void RefreshBindings()
        {
            _header.ClearBindings();

            foreach (var control in _binding.Bindings)
            {
                /* TODO-FRICK: MIDI stuff
                if (control.Control is MidiNoteControl)
                {
                    _header.AddBinding<ReusableSingleMidiNoteBindView, ReusableButtonBinding, ReusableSingleButtonBinding>(
                        _midiNoteViewPrefab, _binding, control);
                }
                else
                {*/
                    _header.AddBinding<ReusableSingleButtonBindView, ReusableButtonBinding, ReusableSingleButtonBinding>(
                        _viewPrefab, _binding, control, _controls);
                //}
            }

            _header.RebuildBindingsLayout();
        }
    }
}

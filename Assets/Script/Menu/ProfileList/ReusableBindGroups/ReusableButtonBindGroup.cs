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
            ProfilesMenu profilesMenu,
            ReusableBindingSet bindingSet,
            ReusableButtonBinding binding,
            List<ControlItemInfo> controls
        )
        {
            base.Init(profilesMenu, bindingSet, binding, controls);

            _debounceSlider.SetValueWithoutNotify(binding.DebounceThreshold);
            _debounceSlider.SetInteractable(!bindingSet.IsHardcoded);
        }

        public void OnDebounceValueChanged()
        {
            Binding.DebounceThreshold = (long)_debounceSlider.Value;
        }

        public override void AddNewBinding()
        {
            Binding.Bindings.Add(new());
            RefreshBindings();
        }
    }
}

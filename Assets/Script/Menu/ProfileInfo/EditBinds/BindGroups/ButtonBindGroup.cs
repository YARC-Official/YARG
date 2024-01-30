using Minis;
using UnityEngine;
using YARG.Input;
using YARG.Player;

namespace YARG.Menu.ProfileInfo
{
    public class ButtonBindGroup : BindGroup<SingleButtonBindView, float, ButtonBinding, SingleButtonBinding>
    {
        [SerializeField]
        private SingleMidiNoteBindView _midiNoteViewPrefab;

        [Space]
        [SerializeField]
        private ButtonDisplay _pressedIndicator;

        [Space]
        [SerializeField]
        private ValueSlider _debounceSlider;

        public override void Init(EditBindsTab editBindsTab, YargPlayer player, ButtonBinding binding)
        {
            base.Init(editBindsTab, player, binding);

            _debounceSlider.SetValueWithoutNotify(binding.DebounceThreshold);
        }

        protected override void OnStateChanged()
        {
            _pressedIndicator.IsPressed = _binding.State;
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
                if (control.Control is MidiNoteControl)
                {
                    _header.AddBinding<SingleMidiNoteBindView, float, ButtonBinding, SingleButtonBinding>(
                        _midiNoteViewPrefab, _binding, control);
                }
                else
                {
                    _header.AddBinding<SingleButtonBindView, float, ButtonBinding, SingleButtonBinding>(
                        _viewPrefab, _binding, control);
                }
            }

            _header.RebuildBindingsLayout();
        }
    }
}
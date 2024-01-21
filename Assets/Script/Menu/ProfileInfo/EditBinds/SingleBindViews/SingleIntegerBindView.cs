using TMPro;
using UnityEngine;
using YARG.Input;

namespace YARG.Menu.ProfileInfo
{
    public class SingleIntegerBindView : SingleBindView<int, IntegerBinding, SingleIntegerBinding>
    {
        [SerializeField]
        private TMP_InputField _valueText;

        public override void Init(IntegerBinding binding, SingleIntegerBinding singleBinding)
        {
            base.Init(binding, singleBinding);

            singleBinding.StateChanged += OnStateChanged;
            OnStateChanged(singleBinding.State);
        }

        private void OnDestroy()
        {
            SingleBinding.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(int state)
        {
            _valueText.text = state.ToString();
        }
    }
}
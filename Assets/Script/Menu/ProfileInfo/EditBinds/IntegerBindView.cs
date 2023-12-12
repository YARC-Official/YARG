using TMPro;
using UnityEngine;
using YARG.Input;

namespace YARG.Menu.ProfileInfo
{
    public class IntegerBindView : BindView<int, IntegerBinding, SingleIntegerBinding>
    {
        [SerializeField]
        private TMP_InputField _valueText;

        public override void Init(EditProfileMenu editProfileMenu, IntegerBinding binding, SingleIntegerBinding singleBinding)
        {
            base.Init(editProfileMenu, binding, singleBinding);

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
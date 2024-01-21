using TMPro;
using UnityEngine;
using YARG.Input;

namespace YARG.Menu.ProfileInfo
{
    public class IntegerBindGroup : BindGroup<SingleIntegerBindView, int, IntegerBinding, SingleIntegerBinding>
    {
        [Space]
        [SerializeField]
        private TMP_InputField _valueText;

        protected override void OnStateChanged()
        {
            _valueText.text = _binding.State.ToString();
        }
    }
}
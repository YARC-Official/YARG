using TMPro;
using Unity.Multiplayer.PlayMode;
using UnityEngine;
using YARG.Helpers.Extensions;
using YARG.Input;
using YARG.Menu.ProfileInfo;

namespace YARG.Menu.ProfileList
{
    public class BindingSetsCenterPane : MonoBehaviour
    {
        [SerializeField]
        private GameObject _contents;
        [SerializeField]
        private Transform _bindsList;
        [SerializeField]
        private TextMeshProUGUI _name;



        [Space]
        [SerializeField]
        private ButtonBindGroup _buttonGroupPrefab;
        [SerializeField]
        private AxisBindGroup _axisGroupPrefab;
        [SerializeField]
        private IntegerBindGroup _integerGroupPrefab;

        private ReusableBindingSet _bindingSet;

        public void HideContents()
        {
            _contents.SetActive(false);
        }

        public void ShowContents()
        {
            _contents.SetActive(true);
        }

        public void OnEnable()
        {
            ClearBindingSet();
        }

        public void ClearBindingSet()
        {
            SelectBindingSet(null);
        }

        public void SelectBindingSet(ReusableBindingSet? bindingSet)
        {
            _bindingSet = bindingSet;

            if (_bindingSet is null)
            {
                HideContents();
                return;
            }

            ShowContents();

            RefreshFromBindingSet(_bindingSet);
        }

        private void RefreshFromBindingSet(ReusableBindingSet bindingSet)
        {
            _bindsList.DestroyChildren();

            _name.text = bindingSet.Name;

            foreach (var (name, binding) in bindingSet.Bindings)
            {
                switch (binding)
                {
                    case ReusableButtonBinding button:
                        var buttonGroup = Instantiate(_buttonGroupPrefab, _bindsList);
                        buttonGroup.Init(this, null, binding);
                        break;

                    case ReusableAxisBinding axis:
                        var axisGroup = Instantiate(_axisGroupPrefab, _bindsList);
                        axisGroup.Init(this, null, axis);
                        break;

                    case ReusableIntegerBinding integer:
                        var integerGroup = Instantiate(_integerGroupPrefab, _bindsList);
                        integerGroup.Init(this, null, integer);
                        break;
                }
            }
        }
    }
}

using TMPro;
using UnityEngine;
using YARG.Helpers.Extensions;
using YARG.Input.Bindings;
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
        }
    }
}

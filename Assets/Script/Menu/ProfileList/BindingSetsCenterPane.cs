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
        private ReusableButtonBindGroup _buttonGroupPrefab;
        [SerializeField]
        private AxisBindGroup _axisGroupPrefab;
        [SerializeField]
        private IntegerBindGroup _integerGroupPrefab;

        private ReusableBindingSet _bindingSet;
        private BindingSetView _bindingSetView;

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
            SelectBindingSet(null, null);
        }

        public void SelectBindingSet(ReusableBindingSet? bindingSet, BindingSetView bindingSetView)
        {
            _bindingSet = bindingSet;
            _bindingSetView = bindingSetView;

            if (_bindingSet is null)
            {
                HideContents();
                return;
            }

            var template = ReusableBindingSetTemplates.GetTemplate(bindingSet.Mode);

            foreach (var (key, info) in template)
            {
                switch (info.Type)
                {
                    case BindingType.Button or BindingType.IndividualButton:
                        var go = Instantiate(_buttonGroupPrefab, transform);
                        go.Init(this, bindingSet, bindingSet.Bindings[key] as ReusableButtonBinding);
                        break;
                }
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

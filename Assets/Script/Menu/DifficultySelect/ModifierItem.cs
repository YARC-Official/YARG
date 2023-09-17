using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using YARG.Menu.Navigation;

namespace YARG.Menu.DifficultySelect
{
    public class ModifierItem : NavigatableBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _title;
        [SerializeField]
        private GameObject _checkedToggle;

        private bool _active;
        public bool Active
        {
            get => _active;
            set
            {
                _active = value;
                _checkedToggle.SetActive(_active);
            }
        }

        private Action<bool> _activeChangedCallback;

        public void Initialize(string title, bool active, Action<bool> activeChangedCallback)
        {
            _title.text = title;
            _activeChangedCallback = activeChangedCallback;

            Active = active;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            Confirm();
        }

        public override void Confirm()
        {
            base.Confirm();

            Active = !Active;

            _activeChangedCallback?.Invoke(Active);
        }
    }
}
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Input;
using YARG.Menu.Data;
using YARG.Player;

namespace YARG.Menu.ProfileInfo
{
    public abstract class BindGroup<TState, TBinding, TSingle> : MonoBehaviour
        where TState : struct
        where TBinding : ControlBinding<TState, TSingle>
        where TSingle : SingleBinding<TState>
    {
        [Space]
        [SerializeField]
        private LocalizeStringEvent _bindingNameText;
        [SerializeField]
        private Image _bindingIcon;

        private EditBindsTab _editBindsTab;
        private YargPlayer _player;
        private TBinding _binding;

        public void Init(EditBindsTab editBindsTab, YargPlayer player, TBinding binding)
        {
            _editBindsTab = editBindsTab;
            _player = player;
            _binding = binding;

            _bindingNameText.StringReference = _binding.Name;

            var icons = MenuData.NavigationIcons;

            if (editBindsTab.SelectingMenuBinds && icons.HasIcon((MenuAction) binding.Action))
            {
                // Show icons for menu actions
                _bindingIcon.gameObject.SetActive(true);

                _bindingIcon.sprite = icons.GetIcon((MenuAction) binding.Action);
                _bindingIcon.color = icons.GetColor((MenuAction) binding.Action);
            }
            else
            {
                // Don't for anything else
                _bindingIcon.gameObject.SetActive(false);
            }
        }

        public async void AddNewBind()
        {
            await _editBindsTab.ShowControlDialog(_player, _binding);
        }
    }
}
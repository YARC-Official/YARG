using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core.Audio;
using YARG.Core.Game;
using YARG.Player;
using YARG.Input;

namespace YARG.Menu.ProfileList
{
    public class MicrophoneEntryView : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _name;

        private YargProfile _profile;
        private ProfileView _profileView;
        private ProfileCenterPane _profileSidebar;
        public MicDevice Microphone { get; private set; }
        public string DisplayName => _name.text;

        private void OnEnable()
        {
            MicrophoneWatcher.MicrophonesChanged += RefreshDisplayName;
            RefreshDisplayName();
        }

        private void OnDisable()
        {
            MicrophoneWatcher.MicrophonesChanged -= RefreshDisplayName;
        }

        public void Initialize(YargProfile profile, ProfileView profileView, ProfileCenterPane profileSidebar, MicDevice microphone)
        {
            _profile = profile;
            _profileView = profileView;
            _profileSidebar = profileSidebar;
            Microphone = microphone;
            RefreshDisplayName();
        }

        private void RefreshDisplayName()
        {
            if (Microphone is not null)
            {
                _name.text = MicrophoneWatcher.GetDisplayName(Microphone.DisplayName, Microphone.Id);
            }
        }

        public void Remove()
        {
            var player = PlayerContainer.GetPlayerFromProfile(_profile);
            player.DeviceInfo.RemoveMicrophone(Microphone.Id);
            _profileSidebar.UpdateCenterPane(_profile, _profileView);
        }
    }
}

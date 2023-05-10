using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using YARG.Settings.Types;

namespace YARG.Settings.Visuals {
	public abstract class AbstractSettingVisual<T> : MonoBehaviour, ISettingVisual where T : ISettingType {
		[SerializeField]
		private LocalizeStringEvent settingText;

		// Select sounds for all settings visuals will go off without PostInit flag
		private bool PostInit { get; set; } 

		protected T Setting { get; private set; }

		public void SetSetting(string name) {
			settingText.StringReference = new LocalizedString {
				TableReference = "Settings",
				TableEntryReference = name
			};

			Setting = (T) SettingsManager.GetSettingByName(name);

			OnSettingInit();
			PostInit = true;
		}

		protected abstract void OnSettingInit();
		protected abstract void RefreshVisual();

		public void PlaySelectSoundEffect() {
			if (!PostInit) {
				return;
			}
			
			GameManager.AudioManager.PlaySoundEffect(AudioManager.Instance.SelectSfx);
		}
	}
}
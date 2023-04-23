using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using YARG.Settings.SettingTypes;

namespace YARG.Settings.SettingVisuals {
	public abstract class AbstractSettingVisual<T> : MonoBehaviour, ISettingVisual where T : ISettingType {
		[SerializeField]
		private LocalizeStringEvent settingText;

		protected T Setting { get; private set; }

		public void SetSetting(string name) {
			settingText.StringReference = new LocalizedString {
				TableReference = "Settings",
				TableEntryReference = name
			};

			Setting = (T) SettingsManager.GetSettingByName(name);

			OnSettingInit();
		}

		protected abstract void OnSettingInit();
		protected abstract void OnSettingChange();
	}
}
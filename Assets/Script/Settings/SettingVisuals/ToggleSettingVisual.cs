using UnityEngine;
using UnityEngine.UI;
using YARG.Settings.SettingTypes;

namespace YARG.Settings.SettingVisuals {
	public class ToggleSettingVisual : AbstractSettingVisual<ToggleSetting> {
		[SerializeField]
		private Toggle toggle;

		protected override void OnSettingInit() {
			toggle.isOn = Setting.Data;
		}

		protected override void OnSettingChange() {
			Setting.Data = toggle.isOn;
		}

		public void OnToggleChange() {
			OnSettingChange();
		}
	}
}
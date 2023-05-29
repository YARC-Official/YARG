using UnityEngine;
using UnityEngine.UI;
using YARG.Settings.Types;

namespace YARG.Settings.Visuals {
	public class ToggleSettingVisual : AbstractSettingVisual<ToggleSetting> {
		[SerializeField]
		private Toggle toggle;

		protected override void OnSettingInit() {
			RefreshVisual();
		}

		public override void RefreshVisual() {
			toggle.isOn = Setting.Data;
		}

		public void OnToggleChange() {
			Setting.Data = toggle.isOn;
			RefreshVisual();
		}
	}
}
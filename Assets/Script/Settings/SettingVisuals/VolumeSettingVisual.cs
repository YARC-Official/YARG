using UnityEngine;
using UnityEngine.UI;
using YARG.Settings.SettingTypes;

namespace YARG.Settings.SettingVisuals {
	public class VolumeSettingVisual : AbstractSettingVisual<VolumeSetting> {
		[SerializeField]
		private Slider slider;

		protected override void OnSettingInit() {
			slider.value = Setting.Data;
		}

		protected override void OnSettingChange() {
			Setting.Data = slider.value;
		}

		public void OnSliderChange() {
			OnSettingChange();
		}
	}
}
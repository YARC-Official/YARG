using System;

namespace YARG.Settings.SettingTypes {
	public class ToggleSetting : AbstractSetting<bool> {
		private bool _data;
		public override bool Data {
			get => _data;
			set {
				base.Data = value;
				_data = value;
			}
		}

		public override string AddressableName => "Setting/Toggle";

		public ToggleSetting(bool value, Action<bool> onChange = null) : base(onChange) {
			Data = value;
		}
	}
}
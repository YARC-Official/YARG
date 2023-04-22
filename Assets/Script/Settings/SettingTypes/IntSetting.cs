using System;
using UnityEngine;

namespace YARG.Settings.SettingTypes {
	public class IntSetting : AbstractSetting<int> {
		private int _data;
		public override int Data {
			get => _data;
			set {
				base.Data = value;
				_data = Mathf.Clamp(value, min, max);
			}
		}

		public override string AddressableName => "Setting/Number";

		private int min;
		private int max;

		public IntSetting(int value, int min = int.MinValue, int max = int.MaxValue, Action<int> onChange = null) : base(onChange) {
			Data = value;
			this.min = min;
			this.max = max;
		}
	}
}
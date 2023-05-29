using System;
using UnityEngine;

namespace YARG.Settings.Types {
	public class IntSetting : AbstractSetting<int> {
		private int _data;
		public override int Data {
			get => _data;
			set {
				_data = Mathf.Clamp(value, Min, Max);
				base.Data = value;
			}
		}

		public override string AddressableName => "Setting/Number";

		public int Min { get; private set; }
		public int Max { get; private set; }

		public IntSetting(int value, int min = int.MinValue, int max = int.MaxValue, Action<int> onChange = null) : base(onChange) {
			Min = min;
			Max = max;

			_data = value;
		}

		public override bool IsSettingDataEqual(object obj) {
			if (obj.GetType() != DataType) {
				return false;
			}

			int a = (int) obj;
			return a == Data;
		}
	}
}
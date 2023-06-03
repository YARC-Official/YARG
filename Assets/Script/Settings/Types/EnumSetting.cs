using System;

namespace YARG.Settings.Types {
	public class EnumSetting : AbstractSetting<int> {
		private int _data;
		public override int Data {
			get => _data;
			set {
				_data = value;
				base.Data = value;
			}
		}

		public Type EnumType { get; private set; }

		public override string AddressableName => "Setting/Enum";

		public EnumSetting(Type enumType, int value, Action<int> onChange = null) : base(onChange) {
			EnumType = enumType;
			_data = value;
		}

		public override bool IsSettingDataEqual(object obj) {
			if (obj.GetType() != EnumType) {
				return false;
			}

			var a = (int) obj;
			return a == _data;
		}
	}
}
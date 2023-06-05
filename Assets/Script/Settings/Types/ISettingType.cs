using System;

namespace YARG.Settings.Types {
	public interface ISettingType {
		public object DataAsObject { get; set; }
		public Type DataType { get; }

		public string AddressableName { get; }

		public void ForceInvokeCallback();
		public bool IsSettingDataEqual(object obj);
	}
}
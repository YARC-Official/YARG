using System;

namespace YARG.Settings.SettingTypes {
	public interface ISettingType {
		public object DataAsObject { get; set; }
		public Type DataType { get; }
		
		public string AddressableName { get; }
	}
}
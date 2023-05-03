using System;
using Newtonsoft.Json;
using YARG.Serialization;

namespace YARG.Settings.Types {
	[JsonConverter(typeof(AbstractSettingConverter))]
	public abstract class AbstractSetting<T> : ISettingType {
		public virtual T Data {
			get => default;
			set {
				onChange?.Invoke(value);
			}
		}

		public object DataAsObject {
			get => Data;
			set => Data = (T) value;
		}
		public Type DataType => GetType().BaseType.GetGenericArguments()[0];

		public abstract string AddressableName { get; }

		protected Action<T> onChange;

		public AbstractSetting(Action<T> onChange) {
			this.onChange = onChange;
		}
	}
}
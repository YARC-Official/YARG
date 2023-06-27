using System;
using Newtonsoft.Json;
using YARG.Serialization;

namespace YARG.Settings.Types {
	[JsonConverter(typeof(AbstractSettingConverter))]
	public abstract class AbstractSetting<T> : ISettingType {
		public virtual T Data {
			get => default;
			set {
				_onChange?.Invoke(value);

				SettingsMenu.Instance.UpdatePresetDropdowns(this);
			}
		}

		public object DataAsObject {
			get => Data;
			set => Data = (T) value;
		}
		public Type DataType => GetType().BaseType?.GetGenericArguments()[0];

		public abstract string AddressableName { get; }

		private readonly Action<T> _onChange;

		protected AbstractSetting(Action<T> onChange) {
			_onChange = onChange;
		}

		public void ForceInvokeCallback() {
			_onChange?.Invoke(Data);
		}

		public abstract bool IsSettingDataEqual(object obj);
	}
}
using System;

namespace YARG.Settings.SettingTypes {
	public abstract class AbstractSetting<T> {
		public virtual T Data {
			get => default;
			set {
				onChange?.Invoke(value);
			}
		}

		public abstract string AddressableName { get; }

		protected Action<T> onChange;

		public AbstractSetting(Action<T> onChange) {
			this.onChange = onChange;
		}
	}
}
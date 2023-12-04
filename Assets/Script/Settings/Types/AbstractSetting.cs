using System;
using Newtonsoft.Json;
using YARG.Menu.Settings;

namespace YARG.Settings.Types
{
    [JsonConverter(typeof(AbstractSettingConverter))]
    public abstract class AbstractSetting<T> : ISettingType
    {
        protected T _value;

        public T Value
        {
            get => _value;
            set
            {
                SetValue(value);

                _onChange?.Invoke(value);

                SettingsMenu.Instance.OnSettingChanged();
            }
        }

        public object ValueAsObject
        {
            get => Value;
            set => Value = (T) value;
        }

        public Type ValueType => typeof(T);

        public abstract string AddressableName { get; }

        private readonly Action<T> _onChange;

        protected AbstractSetting(Action<T> onChange)
        {
            _onChange = onChange;
        }

        protected virtual void SetValue(T value)
        {
            _value = value;
        }

        public void SetValueWithoutNotify(T value) => SetValue(value);

        public void ForceInvokeCallback()
        {
            _onChange?.Invoke(Value);
        }

        public abstract bool ValueEquals(object obj);
    }
}
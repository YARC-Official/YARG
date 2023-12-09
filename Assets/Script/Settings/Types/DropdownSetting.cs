using System;
using System.Collections;
using System.Collections.Generic;

namespace YARG.Settings.Types
{
    // Best we can do to escape the generics in DropdownSettingVisual
    public interface IDropdownSetting : ISettingType
    {
        int Count { get; }
        int CurrentIndex { get; }

        void SelectIndex(int index);
        string IndexToString(int index);
    }

    public class DropdownSetting<T> : AbstractSetting<T>, IDropdownSetting
    {
        public override string AddressableName => "Setting/Dropdown";

        private readonly List<T> _possibleValues;
        public IReadOnlyList<T> PossibleValues => _possibleValues;

        int IDropdownSetting.Count => _possibleValues.Count;

        public int CurrentIndex => _possibleValues.IndexOf(Value);

        public DropdownSetting(List<T> possibleValues, T value, Action<T> onChange = null) :
            base(onChange)
        {
            _possibleValues = possibleValues;
            _value = value;
        }

        public override bool ValueEquals(T value) => Value.Equals(value);

        void IDropdownSetting.SelectIndex(int index) => Value = _possibleValues[index];
        string IDropdownSetting.IndexToString(int index) => ValueToString(_possibleValues[index]);

        public string ValueToString(T value)
        {
            return value.ToString();
        }
    }
}
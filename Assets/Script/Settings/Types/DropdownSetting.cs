using System;
using System.Collections;
using System.Collections.Generic;

namespace YARG.Settings.Types
{
    // Best we can do to escape the generics in DropdownSettingVisual
    public interface IDropdownSetting : ISettingType
    {
        IEnumerable PossibleValues { get; }
        int CurrentIndex { get; }

        object GetAtIndex(int index);
    }

    public class DropdownSetting<T> : AbstractSetting<T>, IDropdownSetting
    {
        public override string AddressableName => "Setting/Dropdown";

        private readonly List<T> _possibleValues;
        public IReadOnlyList<T> PossibleValues => _possibleValues;

        IEnumerable IDropdownSetting.PossibleValues => PossibleValues;

        public int CurrentIndex => _possibleValues.IndexOf(Value);

        public DropdownSetting(List<T> possibleValues, T value, Action<T> onChange = null) :
            base(onChange)
        {
            _possibleValues = possibleValues;
            _value = value;
        }

        public override bool ValueEquals(T value) => Value.Equals(value);

        object IDropdownSetting.GetAtIndex(int index) => _possibleValues[index];
    }
}
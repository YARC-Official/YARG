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

    public class DropdownSetting<T> : AbstractSetting<T>, IDropdownSetting, IEnumerable<T>
    {
        public override string AddressableName => "Setting/Dropdown";

        protected readonly List<T> _possibleValues = new();
        public IReadOnlyList<T> PossibleValues => _possibleValues;

        int IDropdownSetting.Count => _possibleValues.Count;

        public int CurrentIndex => _possibleValues.IndexOf(Value);

        public DropdownSetting(T value, Action<T> onChange = null) :
            base(onChange)
        {
            _value = value;
            UpdateValues();
        }

        public override bool ValueEquals(T value) => Value.Equals(value);

        void IDropdownSetting.SelectIndex(int index) => Value = _possibleValues[index];
        string IDropdownSetting.IndexToString(int index) => ValueToString(_possibleValues[index]);

        public virtual void UpdateValues() { }

        public virtual string ValueToString(T value) => value.ToString();

        // For collection initializer support
        public void Add(T setting) => _possibleValues.Add(setting);
        private List<T>.Enumerator GetEnumerator() => _possibleValues.GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
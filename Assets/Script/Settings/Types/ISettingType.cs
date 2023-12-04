using System;

namespace YARG.Settings.Types
{
    public interface ISettingType
    {
        public object ValueAsObject { get; set; }
        public Type ValueType { get; }

        public string AddressableName { get; }

        public void ForceInvokeCallback();
        public bool ValueEquals(object obj);
    }
}
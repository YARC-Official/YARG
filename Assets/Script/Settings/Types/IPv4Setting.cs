using System;
using System.Net;
using System.Net.Sockets;

namespace YARG.Settings.Types
{
    public class IPv4Setting : AbstractSetting<string>
    {
        public override string AddressableName => "Setting/IPv4";

        private readonly string _defaultValue;

        public bool AllowEmpty { get; }

        public IPv4Setting(string defaultValue, Action<string> onChange = null, bool allowEmpty = false) : base(onChange)
        {
            _defaultValue = defaultValue;
            AllowEmpty = allowEmpty;
            _value = defaultValue;
        }

        protected override void SetValue(string value)
        {
            if (AllowEmpty && string.IsNullOrEmpty(value))
            {
                _value = string.Empty;
                return;
            }

            if (!IsValidIPv4(value))
            {
                _value = _defaultValue;
            }
            else
            {
                _value = value;
            }
        }

        public override bool ValueEquals(string value)
        {
            return value == Value;
        }

        public static bool IsValidIPv4(string ip)
        {
            if (string.IsNullOrEmpty(ip))
            {
                return false;
            }

            if (!IPAddress.TryParse(ip, out var ipAddress))
            {
                return false;
            }

            return IsValidIPv4(ipAddress);
        }

        public static bool IsValidIPv4(IPAddress ip)
        {
            if (ip.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            return true;
        }
    }
}
